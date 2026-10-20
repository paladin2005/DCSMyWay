local status, err = pcall(function()
  local lfs = require('lfs')
  dofile(lfs.writedir() .. [[Scripts\DCSMyWay\DCSMyWay.lua]])
end)

if not status then
  log.write('DCSMYWAY', log.ERROR, 'Failed to load: ' .. tostring(err))
end
