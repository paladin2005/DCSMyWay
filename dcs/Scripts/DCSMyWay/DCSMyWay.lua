local lfs = require('lfs')

package.path = package.path .. ';.\\LuaSocket\\?.lua;.\\Scripts\\?.lua;.\\Scripts\\UI\\?.lua'
package.cpath = package.cpath .. ';.\\LuaSocket\\?.dll'

local socket = require('socket')
local net = require('net')
local dxgui = require('dxgui')
local DialogLoader = require('DialogLoader')
local SkinUtils = require('SkinUtils')
local Terrain = require('terrain')

local VERSION = '0.2.2'
local HOST = '127.0.0.1'
local APP_PORT = 42080
local COMMAND_PORT = 42081
local CAMERA_INTERVAL = 0.1
local BIND_RETRY_INTERVAL = 2
local CLIENT_TIMEOUT = 5
local STATUS_DURATION = 2.5
local MARK_TIMEOUT = 1.5
local CROSSHAIR_COLOR = '0x5aff6eff'
local ACCEPT_IDLE_COLOR = '0xffffffc8'
local ACCEPT_HOVER_COLOR = '0xffffffff'
local ACCEPT_PRESSED_COLOR = '0xb4b4b4ff'
local FOLDER = lfs.writedir() .. [[Scripts\DCSMyWay\]]
local FIRST_MARK_ID = 970001
local ROUTE_SCRIPT = [[
for _, id in ipairs({ %s }) do trigger.action.removeMark(id) end
local color = { 0, 0.9, 1, 1 }
local fill = { 0, 0.9, 1, 0.3 }
local labelFill = { 0, 0, 0, 0.6 }
local points = { %s }
local positions = {}
local id = %d
for index, point in ipairs(points) do
  positions[index] = coord.LLtoLO(point[2], point[3], 0)
  if index > 1 then
    trigger.action.lineToAll(-1, id, positions[index - 1], positions[index], color, 1, true)
    id = id + 1
  end
end
for index, point in ipairs(points) do
  trigger.action.circleToAll(-1, id, positions[index], 200, color, fill, 1, true)
  trigger.action.textToAll(-1, id + 1, positions[index], color, labelFill, 18, true, ' ' .. point[1] .. ' ')
  id = id + 2
end
]]

local udp
local server
local nextBindAt = 0
local client
local clientBuffer = {}
local clientSince = 0
local nextCameraAt = 0
local presses
local pressIndex = 1
local pressed
local releaseAt = 0
local overlay
local acceptWindow
local crosshairVisible = false
local acceptPressed = false
local statusUntil
local markSentAt
local routeMarks = {}
local nextMarkId = FIRST_MARK_ID
local mainView
local mainViewLoaded = false
local lastLayout
local lastError

local function info(message)
  log.write('DCSMYWAY', log.INFO, message)
end

local function report(err)
  local message = tostring(err)
  if message == lastError then return end
  lastError = message
  log.write('DCSMYWAY', log.ERROR, message)
end

local function guarded(action)
  return function(...)
    local succeeded, err = pcall(action, ...)
    if not succeeded then report(err) end
  end
end

local function jsonString(value)
  local escaped = tostring(value):gsub('[%c"\\]', function(character)
    return string.format('\\u%04x', character:byte())
  end)
  return '"' .. escaped .. '"'
end

local function readCamera()
  local camera = Export.LoGetCameraPosition()
  if not camera or not camera.p then return nil end
  local x, z = camera.p.x, camera.p.z
  local converted, latitude, longitude = pcall(Terrain.convertMetersToLatLon, x, z)
  if not converted then return nil end
  local aircraft = Export.LoGetSelfData()
  return {
    model = aircraft and aircraft.Name or 'Spectator',
    latitude = latitude,
    longitude = longitude,
    elevation = Export.LoGetAltitude(x, z),
  }
end

local function positionMessage(messageType, position)
  return string.format(
    '{"type":"%s","model":%s,"lat":%.9f,"lon":%.9f,"elev":%.3f,"crosshair":%s,"busy":%s}',
    messageType,
    jsonString(position.model),
    position.latitude,
    position.longitude,
    position.elevation,
    tostring(crosshairVisible),
    tostring(presses ~= nil))
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
  info('Listening for commands on port ' .. COMMAND_PORT)
end

local function closeSockets()
  closeClient()
  if server then server:close() end
  if udp then udp:close() end
  server = nil
  udp = nil
  nextBindAt = 0
end

local function loadTable(path, environment)
  local chunk = loadfile(path)
  if not chunk then return nil end
  setfenv(chunk, environment)
  chunk()
  return environment
end

local function loadMainView()
  local config = loadTable(lfs.writedir() .. [[Config\options.lua]], {})
  local graphics = config and config.options and config.options.graphics
  if not graphics or not graphics.multiMonitorSetup or not graphics.width or not graphics.height then return nil end
  local screen = { width = graphics.width, height = graphics.height, aspect = graphics.width / graphics.height }
  local folders = { lfs.writedir() .. [[Config\MonitorSetup\]], lfs.currentdir() .. [[\Config\MonitorSetup\]] }
  for _, folder in ipairs(folders) do
    local environment = setmetatable({ _ = function(text) return text end, screen = screen }, { __index = _G })
    local setup = loadTable(folder .. graphics.multiMonitorSetup .. '.lua', environment)
    local viewports = setup and rawget(setup, 'Viewports')
    local view = setup and (rawget(setup, 'UIMainView') or (viewports and viewports.Center))
    if view and view.width and view.height then
      return {
        x = view.x or 0,
        y = view.y or 0,
        width = view.width,
        height = view.height,
        screenWidth = screen.width,
        screenHeight = screen.height,
      }
    end
  end
end

local function screenCenter()
  if not mainViewLoaded then
    mainViewLoaded = true
    local loaded, result = pcall(loadMainView)
    if loaded then mainView = result else report('Cannot read the monitor setup: ' .. tostring(result)) end
  end
  local width, height = dxgui.GetScreenSize()
  if mainView and width == mainView.screenWidth and height == mainView.screenHeight then
    return math.floor(mainView.x + mainView.width / 2), math.floor(mainView.y + mainView.height / 2), width, height
  end
  return math.floor(width / 2), math.floor(height / 2), width, height
end

local function layoutOverlay()
  local x, y, width, height = screenCenter()
  overlay:setBounds(x - 180, y - 140, 360, 280)
  acceptWindow:setBounds(x - 26, y - 100, 52, 52)
  local layout = string.format('Crosshair at %d,%d on a %dx%d screen', x, y, width, height)
  if layout == lastLayout then return end
  lastLayout = layout
  info(layout)
end

local function setPicture(widget, file, color)
  widget:setSkin(SkinUtils.setStaticPicture(FOLDER .. file, widget:getSkin(), color))
end

local function showStatus(text)
  statusUntil = socket.gettime() + STATUS_DURATION
  if overlay then overlay.Status:setText(text) end
end

local function clearStatus()
  statusUntil = nil
  if overlay then overlay.Status:setText('') end
end

local function mark()
  local position = readCamera()
  if not position then
    showStatus('Camera position not available')
    return
  end
  send(positionMessage('mark', position))
  markSentAt = socket.gettime()
  showStatus('Adding point...')
end

local function createOverlay()
  local newOverlay = DialogLoader.spawnDialogFromFile(FOLDER .. 'Overlay.dlg')
  newOverlay:setTransparentForUserInput(true)
  setPicture(newOverlay.Crosshair, [[Images\Crosshair.png]], CROSSHAIR_COLOR)

  local newAcceptWindow = DialogLoader.spawnDialogFromFile(FOLDER .. 'Accept.dlg')
  local accept = newAcceptWindow.Accept
  setPicture(accept, [[Images\Accept.png]], ACCEPT_IDLE_COLOR)
  accept:addMouseEnterCallback(guarded(function()
    setPicture(accept, [[Images\Accept.png]], ACCEPT_HOVER_COLOR)
  end))
  accept:addMouseLeaveCallback(guarded(function()
    acceptPressed = false
    setPicture(accept, [[Images\Accept.png]], ACCEPT_IDLE_COLOR)
  end))
  accept:addMouseDownCallback(guarded(function()
    acceptPressed = true
    setPicture(accept, [[Images\Accept.png]], ACCEPT_PRESSED_COLOR)
  end))
  accept:addMouseUpCallback(guarded(function()
    if not acceptPressed then return end
    acceptPressed = false
    setPicture(accept, [[Images\Accept.png]], ACCEPT_HOVER_COLOR)
    mark()
  end))

  overlay = newOverlay
  acceptWindow = newAcceptWindow
end

local function setCrosshairVisible(visible)
  crosshairVisible = visible
  if visible and not overlay then createOverlay() end
  if not overlay then return end
  if visible then layoutOverlay() else clearStatus() end
  overlay:setVisible(visible)
  acceptWindow:setVisible(visible)
end

local function stopPresses()
  presses = nil
  pressIndex = 1
  pressed = nil
end

local function startPresses(list)
  if presses then
    report('Ignoring waypoints while the previous ones are being entered')
    showStatus('Still entering waypoints')
    return
  end
  if type(list) ~= 'table' or #list == 0 then return end
  presses = list
  pressIndex = 1
  pressed = nil
  setCrosshairVisible(false)
  info('Entering ' .. #list .. ' button presses')
end

local function stepPresses(now)
  if not presses then return end
  if pressed then
    if now < releaseAt then return end
    if pressed.depress then Export.GetDevice(pressed.device):performClickableAction(pressed.code, 0) end
    pressed = nil
    pressIndex = pressIndex + 1
    return
  end
  local press = presses[pressIndex]
  if not press then
    info('Waypoints entered')
    stopPresses()
    return
  end
  Export.GetDevice(press.device):performClickableAction(press.code, press.activate)
  pressed = press
  releaseAt = now + press.delay / 1000
end

local function runInMission(code)
  if not net.dostring_in then
    return false, 'net.dostring_in is not available; allow "userhooks" and "mission" in Config\\autoexec.cfg'
  end
  local wrapped = 'local ok, err = pcall(function() ' .. code .. ' end) if ok then return "ok" end '
    .. 'env.error("DCSMYWAY: " .. tostring(err), false) return "error: " .. tostring(err)'
  local request = 'local a, b = a_do_script(' .. string.format('%q', wrapped) .. ') return tostring(a) .. " " .. tostring(b)'
  local called, result = pcall(net.dostring_in, 'mission', request)
  result = tostring(result)
  if not called then return false, result end
  local failure = result:match('error: .*') or result:match('[Ii]nvalid state.*')
  if failure then return false, failure end
  return true
end

local function drawRoute(points)
  if type(points) ~= 'table' then return end
  local entries = {}
  for _, point in ipairs(points) do
    if type(point.lat) == 'number' and type(point.lon) == 'number' then
      table.insert(entries, string.format('{ %q, %.9f, %.9f }', tostring(point.name or ''), point.lat, point.lon))
    end
  end
  local code = string.format(ROUTE_SCRIPT, table.concat(routeMarks, ', '), table.concat(entries, ', '), nextMarkId)
  local drawn, err = runInMission(code)
  routeMarks = {}
  for _ = 1, #entries * 3 - math.min(#entries, 1) do
    table.insert(routeMarks, nextMarkId)
    nextMarkId = nextMarkId + 1
  end
  if not drawn then report('Cannot draw the route on the map: ' .. tostring(err)) end
end

local function handleCommand(line)
  local parsed, command = pcall(net.json2lua, line)
  if not parsed or type(command) ~= 'table' then
    report('Invalid command: ' .. string.sub(line, 1, 120))
    return
  end
  if command.type == 'crosshair' then
    setCrosshairVisible(command.visible == true)
  elseif command.type == 'waypoints' then
    startPresses(command.presses)
  elseif command.type == 'status' then
    markSentAt = nil
    showStatus(tostring(command.text or ''))
  elseif command.type == 'route' then
    drawRoute(command.points)
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
    showStatus('Waypoint entry failed')
  end

  if now >= nextCameraAt then
    nextCameraAt = now + CAMERA_INTERVAL
    local position = readCamera()
    if position then send(positionMessage('camera', position)) end
  end

  if markSentAt and now - markSentAt > MARK_TIMEOUT then
    markSentAt = nil
    showStatus('DCSMyWay is not running')
  end
  if statusUntil and now >= statusUntil then clearStatus() end
end

local callbacks = {}

callbacks.onSimulationFrame = guarded(frame)

callbacks.onSimulationStop = guarded(function()
  stopPresses()
  setCrosshairVisible(false)
  closeSockets()
  markSentAt = nil
  routeMarks = {}
  lastError = nil
end)

DCS.setUserCallbacks(callbacks)
info('Loaded ' .. VERSION)
