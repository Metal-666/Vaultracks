const openDatabaseDialogBarrier = document.getElementById(
  "open-database-dialog-barrier",
) as HTMLElement;
const usernameInput = document.getElementById(
  "username-input",
) as HTMLInputElement;
const dbKeyInput = document.getElementById("db-key-input") as HTMLInputElement;
const locationPopupContentTemplate = document.getElementById(
  "location-popup-content-template",
) as HTMLTemplateElement;

const map = L.map("map").setView([51.505, -0.09], 13);

L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
  maxZoom: 19,
  attribution:
    '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
}).addTo(map);

async function openDatabase() {
  const response = await fetch("api/location/latest", {
    headers: {
      "Authorization": `Basic ${
        btoa(`${usernameInput.value}:${dbKeyInput.value}`)
      }`,
    },
  });

  if (!response.ok) {
    showToast(
      {
        text: await response.text(),
        duration: -1,
        newWindow: true,
        close: true,
        gravity: "bottom",
        position: "center",
      },
      "bg-gradient-to-r",
      "from-red",
      "to-maroon",
      "text-crust",
      "[&_*]:text-crust",
      "cursor-default",
      "[&_*]:opacity-100",
    );

    return;
  }

  const location = await response.json() as OTLocation;

  const latLng: L.LatLngExpression = [location.lat, location.lon];

  const marker = L.marker(latLng).addTo(map);

  const locationPopupContent = locationPopupContentTemplate.content
    .cloneNode(true) as DocumentFragment;

  const dateAndTime = locationPopupContent.getElementById(
    "date-and-time",
  ) as HTMLElement;

  dateAndTime.textContent = new Date(location.created_at * 1000).toUTCString();
  dateAndTime.id = "";

  marker.bindPopup(locationPopupContent.getRootNode() as HTMLElement, {
    className: "map-popup",
  })
    .openPopup();

  map.flyTo(latLng, 15);

  openDatabaseDialogBarrier.classList.add("hidden");
}

function showToast(options: Toastify.Options, ...classNames: string[]) {
  const toast = Toastify(options);

  toast.showToast();

  toast.toastElement?.classList.add(...classNames);
}

interface OTLocation {
  bssid: string;
  ssid: string;
  _id: string;
  acc: number;
  alt: number;
  batt: number;
  bs: number;
  cog: number;
  conn: string;
  created_at: number;
  lat: number;
  lon: number;
  rad: number;
  t: string;
  m: number;
  tid: string;
  topic: string;
  tst: number;
  vac: number;
  vel: number;
  p: number;
  poi: string;
  tag: string;
  inregions: string[];
  inrids: string[];
}
