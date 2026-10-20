local lfs = require('lfs')

package.path = package.path .. ';' .. lfs.currentdir() .. '/LuaSocket/?.lua'
package.cpath = package.cpath .. ';' .. lfs.currentdir() .. '/LuaSocket/?.dll'

local socket = require('socket')
local JSON = loadfile(lfs.currentdir() .. '/Scripts/JSON.lua')()

local VERSION = '0.7.0'
local HOST = '127.0.0.1'
local APP_PORT = 42080
local COMMAND_PORT = 42082
local STATE_INTERVAL = 0.1
local STEERPOINT_INTERVAL = 0.5
local BIND_RETRY_INTERVAL = 2
local CLIENT_TIMEOUT = 5
local DED_INDICATOR = 6

local udp
local server
local nextBindAt = 0
local client
local clientBuffer = {}
local clientSince = 0
local nextStateAt = 0
local nextSteerpointAt = 0
local presses
local pressIndex = 1
local pressed
local releaseAt = 0
local transferId
local donePresses = 0
local steerpoint
local unknownDedLogged = false
local lastError

local upstreamStart = LuaExportStart
local upstreamBeforeNextFrame = LuaExportBeforeNextFrame
local upstreamStop = LuaExportStop

local function info(message)
  log.write('DCSMYWAY', log.INFO, message)
end

local function report(err)
  local message = tostring(err)
  if message == lastError then return end
  lastError = message
  log.write('DCSMYWAY', log.ERROR, message)
end

local function callUpstream(upstream, name)
  if not upstream then return end
  local succeeded, err = pcall(upstream)
  if not succeeded then report('Error in the ' .. name .. ' of another export script: ' .. tostring(err)) end
end

local function jsonString(value)
  local escaped = tostring(value):gsub('[%c"\\]', function(character)
    return string.format('\\u%04x', character:byte())
  end)
  return '"' .. escaped .. '"'
end

local function parseIndication(text)
  local elements = {}
  local name
  local expectingName = false
  for line in (text .. '\n'):gmatch('([^\n]*)\n') do
    line = line:gsub('\r$', '')
    if line:match('^%-%-%-%-%-+$') then
      expectingName = true
      name = nil
    elseif expectingName then
      name = line
      expectingName = false
    elseif name and elements[name] == nil then
      elements[name] = line
    end
  end
  return elements
end

local function readSteerpoint()
  local read, text = pcall(list_indication, DED_INDICATOR)
  if not read then
    report('Cannot read the DED: ' .. tostring(text))
    return nil
  end
  if type(text) ~= 'string' then return nil end
  local elements = parseIndication(text)
  local number = elements['Selected Steerpoint'] or elements['STEERPOINT NUMBER']
  local current = number and tonumber(number:match('%d+'))
  if not current and text ~= '' and not unknownDedLogged then
    unknownDedLogged = true
    info('DED page without steerpoint: ' .. text:gsub('\n', ' | '):sub(1, 400))
  end
  return current
end

local function aircraftModel()
  local read, aircraft = pcall(LoGetSelfData)
  return read and type(aircraft) == 'table' and aircraft.Name or nil
end

local function updateSteerpoint(now, model)
  if now < nextSteerpointAt then return end
  nextSteerpointAt = now + STEERPOINT_INTERVAL
  local current = (model == nil or model:find('^F%-16')) and readSteerpoint() or nil
  if current == steerpoint then return end
  steerpoint = current
  info('Active steerpoint is now ' .. (steerpoint and tostring(steerpoint) or 'unknown'))
end

local function stateMessage(model)
  return string.format(
    '{"type":"cockpit","model":%s,"steerpoint":%s,"busy":%s,"transferId":%s,"done":%d}',
    model and jsonString(model) or 'null',
    steerpoint and string.format('%d', steerpoint) or 'null',
    tostring(presses ~= nil),
    transferId and string.format('%d', transferId) or 'null',
    donePresses)
end

local function send(message)
  if udp then udp:sendto(message, HOST, APP_PORT) end
end

local function closeClient()
  if client then client:close() end
  client = nil
  clientBuffer = {}
end

local function openSockets(now)
  if not udp then
    udp = socket.udp()
    udp:settimeout(0)
  end
  if server or now < nextBindAt then return end
  nextBindAt = now + BIND_RETRY_INTERVAL
  local candidate = socket.tcp()
  local bound, err = candidate:bind(HOST, COMMAND_PORT)
  if not bound then
    candidate:close()
    report('Cannot listen on port ' .. COMMAND_PORT .. ': ' .. tostring(err))
    return
  end
  candidate:listen(4)
  candidate:settimeout(0)
  server = candidate
  info('Listening for cockpit commands on port ' .. COMMAND_PORT)
end

local function closeSockets()
  closeClient()
  if server then server:close() end
  if udp then udp:close() end
  server = nil
  udp = nil
  nextBindAt = 0
end

local function stopPresses()
  presses = nil
  pressIndex = 1
  pressed = nil
end

local function startPresses(list, id)
  if presses then
    report('Ignoring waypoints while the previous ones are being entered')
    return
  end
  if type(list) ~= 'table' or #list == 0 then return end
  presses = list
  pressIndex = 1
  pressed = nil
  transferId = type(id) == 'number' and id or nil
  donePresses = 0
  info('Entering ' .. #list .. ' button presses')
end

local function cancelPresses()
  if not presses then return end
  if pressed and pressed.depress then GetDevice(pressed.device):performClickableAction(pressed.code, 0) end
  info(string.format('Waypoint entry cancelled after %d of %d button presses', pressIndex - 1, #presses))
  stopPresses()
end

local function stepPresses(now)
  if not presses then return end
  if pressed then
    if now < releaseAt then return end
    if pressed.depress then GetDevice(pressed.device):performClickableAction(pressed.code, 0) end
    pressed = nil
    pressIndex = pressIndex + 1
    donePresses = pressIndex - 1
    return
  end
  local press = presses[pressIndex]
  if not press then
    info('Waypoints entered')
    stopPresses()
    return
  end
  GetDevice(press.device):performClickableAction(press.code, press.activate)
  pressed = press
  releaseAt = now + press.delay / 1000
end

local function handleCommand(line)
  local parsed, command = pcall(JSON.decode, JSON, line)
  if not parsed or type(command) ~= 'table' then
    report('Invalid command: ' .. string.sub(line, 1, 120))
    return
  end
  if command.type == 'waypoints' then
    startPresses(command.presses, command.id)
  elseif command.type == 'cancel' then
    cancelPresses()
  else
    report('Unknown command: ' .. tostring(command.type))
  end
end

local function pollCommands(now)
  if not server then return end
  if not client then
    client = server:accept()
    if not client then return end
    client:settimeout(0)
    clientSince = now
  end
  local line, err, partial = client:receive('*l')
  if partial and partial ~= '' then table.insert(clientBuffer, partial) end
  if line then
    table.insert(clientBuffer, line)
  elseif err == 'timeout' then
    if now - clientSince > CLIENT_TIMEOUT then closeClient() end
    return
  end
  local command = table.concat(clientBuffer)
  closeClient()
  if command ~= '' then handleCommand(command) end
end

local function frame()
  local now = socket.gettime()
  openSockets(now)
  pollCommands(now)

  local stepped, err = pcall(stepPresses, now)
  if not stepped then
    stopPresses()
    report('Waypoint entry failed: ' .. tostring(err))
  end

  if now >= nextStateAt then
    nextStateAt = now + STATE_INTERVAL
    local model = aircraftModel()
    updateSteerpoint(now, model)
    send(stateMessage(model))
  end
end

function LuaExportStart()
  callUpstream(upstreamStart, 'start')
  info('Export loaded ' .. VERSION)
end

function LuaExportBeforeNextFrame()
  callUpstream(upstreamBeforeNextFrame, 'frame')
  local ran, err = pcall(frame)
  if not ran then report(err) end
end

function LuaExportStop()
  callUpstream(upstreamStop, 'stop')
  local stopped, err = pcall(function()
    stopPresses()
    closeSockets()
  end)
  if not stopped then report(err) end
end
