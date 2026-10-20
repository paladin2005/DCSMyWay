const routeColor = "#00e6ff";
const pendingColor = "#9ca3af";
const cameraColor = "#5aff6e";

const baseLayers = {
  OpenTopoMap: L.tileLayer("https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png", {
    maxZoom: 17,
    subdomains: "abc",
    attribution:
      'Map data &copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors, SRTM | ' +
      'Style &copy; <a href="https://opentopomap.org">OpenTopoMap</a> (CC-BY-SA)',
  }),
  OpenStreetMap: L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
  }),
};

const map = L.map("map", { worldCopyJump: true }).setView([42, 43], 7);
const savedLayer = readSetting("layer");
(baseLayers[savedLayer] ?? baseLayers.OpenTopoMap).addTo(map);
L.control.layers(baseLayers, null, { position: "topright" }).addTo(map);
map.on("baselayerchange", (event) => writeSetting("layer", event.name));

const route = L.polyline([], { color: routeColor, weight: 3, opacity: 0.9 }).addTo(map);
const waypointLayer = L.layerGroup().addTo(map);
const camera = L.circleMarker([0, 0], { radius: 7, color: cameraColor, weight: 3, fillOpacity: 0.15 });

let waypoints = [];
let waypointsKey = "";
let cameraPosition = null;
let fitted = false;

const FitControl = L.Control.extend({
  options: { position: "topleft" },
  onAdd() {
    const button = L.DomUtil.create("button", "fit-button leaflet-bar");
    button.type = "button";
    button.textContent = "Fit route";
    button.title = "Show the whole route";
    L.DomEvent.disableClickPropagation(button);
    L.DomEvent.on(button, "click", () => fitRoute(true));
    return button;
  },
});
new FitControl().addTo(map);

function readSetting(name) {
  try {
    return localStorage.getItem(name);
  } catch {
    return null;
  }
}

function writeSetting(name, value) {
  try {
    localStorage.setItem(name, value);
  } catch {}
}

function label(waypoint) {
  const name = document.createElement("span");
  name.textContent = waypoint.steerpoint == null ? waypoint.name : `${waypoint.name} · STPT ${waypoint.steerpoint}`;
  return name;
}

function drawWaypoints() {
  waypointLayer.clearLayers();
  route.setLatLngs(waypoints.map((waypoint) => [waypoint.lat, waypoint.lon]));
  for (const waypoint of waypoints) {
    const entered = waypoint.steerpoint != null;
    L.circleMarker([waypoint.lat, waypoint.lon], {
      radius: 7,
      color: "#0b1220",
      weight: 2,
      fillColor: entered ? routeColor : pendingColor,
      fillOpacity: 1,
    })
      .bindTooltip(label(waypoint), {
        permanent: true,
        direction: "right",
        offset: [10, 0],
        className: entered ? "waypoint-label" : "waypoint-label pending",
      })
      .addTo(waypointLayer);
  }
}

function drawCamera() {
  if (cameraPosition == null) {
    camera.remove();
    return;
  }
  camera.setLatLng([cameraPosition.lat, cameraPosition.lon]);
  if (!map.hasLayer(camera)) camera.addTo(map);
}

function fitRoute(animate) {
  const points = waypoints.map((waypoint) => [waypoint.lat, waypoint.lon]);
  if (points.length > 1) {
    map.fitBounds(points, { padding: [60, 60], maxZoom: 12, animate });
  } else if (points.length === 1) {
    map.setView(points[0], 11, { animate });
  } else if (cameraPosition != null) {
    map.setView([cameraPosition.lat, cameraPosition.lon], 9, { animate });
  } else {
    return false;
  }
  return true;
}

function update(state) {
  const nextKey = JSON.stringify(state.waypoints);
  if (nextKey !== waypointsKey) {
    waypoints = state.waypoints;
    waypointsKey = nextKey;
    drawWaypoints();
  }
  cameraPosition = state.camera;
  drawCamera();
  if (!fitted) fitted = fitRoute(false);
}

window.chrome?.webview?.addEventListener("message", (event) => update(event.data));
