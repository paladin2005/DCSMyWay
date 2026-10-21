local lfs = require('lfs')

package.path = package.path .. ';' .. lfs.currentdir() .. '/LuaSocket/?.lua'
package.cpath = package.cpath .. ';' .. lfs.currentdir() .. '/LuaSocket/?.dll'

local socket = require('socket')
local JSON = loadfile(lfs.currentdir() .. '/Scripts/JSON.lua')()

local VERSION = '0.12.0'
local HOST = '127.0.0.1'
local APP_PORT = 42080
local COMMAND_PORT = 42082
local STATE_INTERVAL = 0.1
local STEERPOINT_INTERVAL = 0.5
local BIND_RETRY_INTERVAL = 2
local CLIENT_TIMEOUT = 5
local CHECK_INTERVAL = 0.05
local ARGUMENT_TOLERANCE = 0.03
local STEERPOINT_SOURCES = {
  { model = '^F%-16', indicators = { 6 }, elements = { 'Selected Steerpoint', 'STEERPOINT NUMBER', 'INS_SelectedSteerpoint' }, display = 'DED' },
  { model = '^FA%-18', indicators = { 4, 2, 3 }, elements = { 'WYPT_Page_Number' } },
  { model = '^A%-10C', indicators = { 3 }, elements = { 'CurrSteerPointNumber' } },
  { model = '^AH%-64', seatIndicators = { [0] = { 9, 7 }, [1] = { 13, 11 } }, elements = { 'NextWaypointStatusWindow_text_1' }, pattern = '^W(%d+)$' },
  { model = '^Ka%-50', indicators = { 5 }, elements = { 'txt_OIT_PPM' }, requires = 'txt_VIT' },
  { model = '^Mirage%-F1EE', argument = 1361, scale = 10 },
}
local SEAT_MODELS = {
  '^AH%-64',
}

local udp
local server
local nextBindAt = 0
local client
local clientBuffer = {}
local clientSince = 0
local nextStateAt = 0
local nextSteerpointAt = 0
local steps
local stepIndex = 1
local phase
local pressed
local releaseAt = 0
local deadline = 0
local nextCheckAt = 0
local stepPresses = 0
local selectedNumber
local otherwisePressed = false
local transferId
local doneSteps = 0
local failure
local steerpoint
local seat
local unknownDisplayLogged = false
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

local function readIndication(indicator)
  local read, text = pcall(list_indication, indicator)
  if not read then
    report('Cannot read indicator ' .. indicator .. ': ' .. tostring(text))
    return nil
  end
  if type(text) ~= 'string' then return nil end
  return text
end

local function readElements(indicator)
  local text = readIndication(indicator)
  return text and parseIndication(text) or {}
end

local function readArgument(argument)
  local read, device = pcall(GetDevice, 0)
  if not read or not device then return nil end
  local ok, value = pcall(device.get_argument_value, device, argument)
  return ok and type(value) == 'number' and value or nil
end

local function readSourceSteerpoint(source, logUnknown)
  if source.argument then
    local value = readArgument(source.argument)
    local current = value and math.floor(value * source.scale + 0.5)
    return current and current > 0 and current or nil
  end
  local indicators = source.seatIndicators and source.seatIndicators[seat or 0] or source.indicators
  for _, indicator in ipairs(indicators) do
    local text = readIndication(indicator)
    if text then
      local elements = parseIndication(text)
      local shown = not source.requires or elements[source.requires]
      for _, name in ipairs(shown and source.elements or {}) do
        local current = elements[name] and tonumber(elements[name]:match(source.pattern or '%d+'))
        if current then return current end
      end
      if logUnknown and source.display and text ~= '' and not unknownDisplayLogged then
        unknownDisplayLogged = true
        info(source.display .. ' page without steerpoint: ' .. text:gsub('\n', ' | '):sub(1, 400))
      end
    end
  end
  return nil
end

local function readSteerpoint(model)
  for _, source in ipairs(STEERPOINT_SOURCES) do
    if model == nil or model:find(source.model) then
      local current = readSourceSteerpoint(source, model ~= nil)
      if current then return current end
    end
  end
  return nil
end

local function aircraftModel()
  local read, aircraft = pcall(LoGetSelfData)
  return read and type(aircraft) == 'table' and aircraft.Name or nil
end

local function readSeat(model)
  local hasSeats = false
  for _, pattern in ipairs(SEAT_MODELS) do
    if model and model:find(pattern) then hasSeats = true end
  end
  if not hasSeats then return nil end
  local read, params = pcall(list_cockpit_params)
  if not read or type(params) ~= 'string' then return nil end
  local value = tonumber(('\n' .. params):match('\n%s*SEAT:%s*([%-%d%.]+)'))
  return value and math.floor(value + 0.5) or nil
end

local function updateSteerpoint(now, model)
  if now < nextSteerpointAt then return end
  nextSteerpointAt = now + STEERPOINT_INTERVAL
  local currentSeat = readSeat(model)
  if currentSeat ~= seat then
    seat = currentSeat
    info('Seat is now ' .. (seat and tostring(seat) or 'unknown'))
  end
  local current = readSteerpoint(model)
  if current == steerpoint then return end
  steerpoint = current
  info('Active steerpoint is now ' .. (steerpoint and tostring(steerpoint) or 'unknown'))
end

local function stateMessage(model)
  return string.format(
    '{"type":"cockpit","model":%s,"steerpoint":%s,"busy":%s,"transferId":%s,"done":%d,"failed":%s,"seat":%s}',
    model and jsonString(model) or 'null',
    steerpoint and string.format('%d', steerpoint) or 'null',
    tostring(steps ~= nil),
    transferId and string.format('%d', transferId) or 'null',
    doneSteps,
    failure and jsonString(failure) or 'null',
    seat and string.format('%d', seat) or 'null')
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

local function stopSteps()
  steps = nil
  stepIndex = 1
  phase = nil
  pressed = nil
end

local function startSteps(list, id)
  if steps then
    report('Ignoring waypoints while the previous ones are being entered')
    return
  end
  if type(list) ~= 'table' or #list == 0 then return end
  steps = list
  stepIndex = 1
  phase = nil
  pressed = nil
  transferId = type(id) == 'number' and id or nil
  doneSteps = 0
  failure = nil
  info('Entering waypoints in ' .. #list .. ' steps')
end

local function release()
  if pressed and pressed.depress then GetDevice(pressed.device):performClickableAction(pressed.code, 0) end
  pressed = nil
end

local function cancelSteps()
  if not steps then return end
  release()
  info(string.format('Waypoint entry cancelled after %d of %d steps', stepIndex - 1, #steps))
  stopSteps()
end

local function argumentsHold(check)
  for index, argument in ipairs(check.arguments) do
    local value = readArgument(argument)
    if not value or math.abs(value - check.levels[index]) > ARGUMENT_TOLERANCE then return false end
  end
  return true
end

local function checkHolds(check, displays)
  if check.arguments then return argumentsHold(check) end
  local elements = displays[check.indicator]
  if not elements then
    elements = readElements(check.indicator)
    displays[check.indicator] = elements
  end
  for name, value in pairs(elements) do
    if name:find(check.element) then
      if check.values then
        for _, expected in ipairs(check.values) do
          if value == expected then return true end
        end
      elseif not check.pattern or value:find(check.pattern) then
        return true
      end
    end
  end
  return false
end

local function anyHolds(checks)
  local displays = {}
  for _, check in ipairs(checks) do
    if checkHolds(check, displays) then return true end
  end
  return false
end

local function readNumber(selection)
  for name, value in pairs(readElements(selection.indicator)) do
    if name:find(selection.element) then return tonumber(value:match('%d+')) end
  end
  return nil
end

local function optionChecks(step)
  local checks = {}
  for _, option in ipairs(step.options) do table.insert(checks, option.when) end
  return checks
end

local function describeChecks(step)
  local checks = step['until'] or step.wait or (step.options and optionChecks(step)) or { step.select }
  local parts = {}
  for _, check in ipairs(checks) do
    local found = false
    for _, argument in ipairs(check.arguments or {}) do
      local value = readArgument(argument)
      table.insert(parts, 'argument ' .. argument .. ' = ' .. (value and string.format('%.3f', value) or '?'))
      found = true
    end
    for name, value in pairs(check.arguments and {} or readElements(check.indicator)) do
      if name:find(check.element) then
        table.insert(parts, name .. ' = ' .. value)
        found = true
      end
    end
    if not found then table.insert(parts, tostring(check.element) .. ' not shown') end
  end
  return table.concat(parts, ', ')
end

local function finishStep()
  stepIndex = stepIndex + 1
  doneSteps = stepIndex - 1
  phase = nil
end

local function failStep(step)
  failure = step.failure or ('Step ' .. stepIndex .. ' did not complete')
  report(string.format('Waypoint entry stopped at step %d of %d: %s (%s)', stepIndex, #steps, failure, describeChecks(step)))
  release()
  stopSteps()
end

local function pressButton(button, now)
  GetDevice(button.device):performClickableAction(button.code, button.activate)
  pressed = button
  releaseAt = now + button.delay / 1000
  stepPresses = stepPresses + 1
  phase = 'holding'
end

local function selectNumber(step, now)
  local number = readNumber(step.select)
  if number == step.select.target then return finishStep() end
  if number == nil or stepPresses >= (step.tries or 1) then return failStep(step) end
  selectedNumber = number
  pressButton(number < step.select.target and step or step.select.down, now)
end

local function firstOption(step)
  local displays = {}
  for _, option in ipairs(step.options) do
    if checkHolds(option.when, displays) then return option end
  end
  return nil
end

local function chooseOption(option, now)
  if not option.device then return finishStep() end
  otherwisePressed = false
  pressButton(option, now)
end

local function pressOtherwise(step, now)
  otherwisePressed = true
  pressButton(step.otherwise, now)
end

local function searchOptions(step, now)
  phase = 'searching'
  deadline = now + (step.timeout or 0) / 1000
  nextCheckAt = now
end

local function stepOptions(step, now)
  if phase == nil then
    stepPresses = 0
    local option = firstOption(step)
    if option then return chooseOption(option, now) end
    if step.otherwise and (step.tries or 1) > 0 then return pressOtherwise(step, now) end
    return searchOptions(step, now)
  end
  if phase == 'holding' then
    if now < releaseAt then return end
    release()
    if not otherwisePressed then return finishStep() end
    return searchOptions(step, now)
  end
  if now < nextCheckAt then return end
  nextCheckAt = now + CHECK_INTERVAL
  local option = firstOption(step)
  if option then return chooseOption(option, now) end
  if now < deadline then return end
  if step.otherwise and stepPresses < (step.tries or 1) then return pressOtherwise(step, now) end
  failStep(step)
end

local function startStep(step, now)
  stepPresses = 0
  if step.select then return selectNumber(step, now) end
  if step['until'] and anyHolds(step['until']) then return finishStep() end
  if step.device then return pressButton(step, now) end
  if not step.wait then return finishStep() end
  phase = 'waiting'
  deadline = now + (step.timeout or 0) / 1000
  nextCheckAt = now
end

local function stepTransfer(now)
  if not steps then return end
  local step = steps[stepIndex]
  if not step then
    info('Waypoints entered')
    stopSteps()
    return
  end
  if phase == nil and step.unless and anyHolds(step.unless) then return finishStep() end
  if step.options then return stepOptions(step, now) end
  if phase == nil then return startStep(step, now) end
  if phase == 'holding' then
    if now < releaseAt then return end
    release()
    if not (step.select or step['until'] or step.wait) then return finishStep() end
    phase = 'waiting'
    deadline = now + (step.timeout or 0) / 1000
    nextCheckAt = now
    return
  end
  if now < nextCheckAt then return end
  nextCheckAt = now + CHECK_INTERVAL
  if step.select then
    if readNumber(step.select) ~= selectedNumber then return selectNumber(step, now) end
  elseif anyHolds(step['until'] or step.wait) then
    return finishStep()
  end
  if now < deadline then return end
  if step['until'] and stepPresses < (step.tries or 1) then return pressButton(step, now) end
  failStep(step)
end

local function handleCommand(line)
  local parsed, command = pcall(JSON.decode, JSON, line)
  if not parsed or type(command) ~= 'table' then
    report('Invalid command: ' .. string.sub(line, 1, 120))
    return
  end
  if command.type == 'waypoints' then
    startSteps(command.steps, command.id)
  elseif command.type == 'cancel' then
    cancelSteps()
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

  local stepped, err = pcall(stepTransfer, now)
  if not stepped then
    stopSteps()
    failure = 'DCSMyWay script error'
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
    stopSteps()
    closeSockets()
  end)
  if not stopped then report(err) end
end
