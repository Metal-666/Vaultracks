const currentCoordinates = document.getElementById(
  "current-coordinates",
) as HTMLInputElement;
const currentTimestamp = document.getElementById(
  "current-timestamp",
) as HTMLInputElement;

const followMarkerCheckbox = document.getElementById(
  "follow-marker-checkbox",
) as HTMLInputElement;
const mapZoomSlider = document.getElementById(
  "map-zoom-slider",
) as HTMLInputElement;

const openDatabaseDialogBarrier = document.getElementById(
  "open-database-dialog-barrier",
) as HTMLElement;
const usernameInput = document.getElementById(
  "username-input",
) as HTMLInputElement;
const dbKeyInput = document.getElementById("db-key-input") as HTMLInputElement;

const map = L.map("map").setView([51.505, -0.09], 13);

L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
  maxZoom: 19,
  attribution:
    '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
}).addTo(map);

let marker: L.Marker | undefined;

function mapZoomChanged() {
  map.setZoom(mapZoomSlider.valueAsNumber);
}

async function openDatabase() {
  console.log("Opening db...");

  const webSocket = new WebSocket(
    `/ws?username=${encodeURIComponent(usernameInput.value)}&databaseKey=${
      encodeURIComponent(dbKeyInput.value)
    }`,
  );

  let keepAliveInterval: number | undefined;

  webSocket.addEventListener("open", () => {
    console.log("WS connection established!");

    openDatabaseDialogBarrier.classList.add("hidden");

    keepAliveInterval = setInterval(() => {
      sendWsMessage(webSocket, {
        command: WebSocketCommand.ping,
      });
    }, 15 * 1000);

    sendWsMessage(webSocket, {
      command: WebSocketCommand.subscribeToLocationUpdates,
    });
  });

  webSocket.addEventListener("close", () => {
    console.warn("WS connection closed!");

    showErrorToast(
      "Something went really wrong!\nConnection to server was terminated!",
    );

    if (keepAliveInterval == null) {
      return;
    }

    clearInterval(keepAliveInterval);
  });

  webSocket.addEventListener("message", (event: MessageEvent<string>) => {
    console.debug(`Received message: `, event.data);

    const message = JSON.parse(event.data) as WebSocketMessage;

    if (message.command == null) {
      sendWsError(webSocket, "Command was not provided!");

      return;
    }

    switch (message.command) {
      case WebSocketCommand.error: {
        showErrorToast(
          `${message.errorMessage}${
            message.errorDescription == null
              ? ""
              : `\n${message.errorDescription}`
          }`,
        );

        break;
      }
      case WebSocketCommand.locationUpdate: {
        if (message.locationJson == null) {
          sendWsError(
            webSocket,
            "Received message with missing property!",
            "(locationJson)",
          );

          break;
        }

        const location = JSON.parse(message.locationJson) as OTLocation;

        if (location.lat == null || location.lon == null) {
          sendWsError(
            webSocket,
            "Latitude and/or longitude missing from payload!",
          );

          break;
        }

        const latLng: L.LatLngExpression = [location.lat, location.lon];

        if (marker == null) {
          marker = L.marker(latLng).addTo(map);
        } else {
          marker.setLatLng(latLng);
        }

        currentCoordinates.value = `${location.lat}, ${location.lon}`;
        currentTimestamp.value = location.created_at == null
          ? "unknown"
          : new Date(location.created_at * 1000).toUTCString();

        if (followMarkerCheckbox.checked) {
          map.flyTo(latLng, mapZoomSlider.valueAsNumber);
        }

        break;
      }
    }
  });
}

function sendWsMessage(
  webSocket: WebSocket,
  webSocketMessage: WebSocketMessage,
) {
  const jsonData = JSON.stringify(webSocketMessage);

  console.debug(`Sending WS message:`, jsonData);

  webSocket.send(jsonData);
}

function sendWsError(
  webSocket: WebSocket,
  message: string,
  description?: string,
) {
  sendWsMessage(webSocket, {
    command: WebSocketCommand.error,
    errorMessage: message,
    errorDescription: description,
  });
}

function showToast(options: Toastify.Options, ...classNames: string[]) {
  const toast = Toastify(options);

  toast.showToast();

  toast.toastElement?.classList.add(...classNames);
}

function showErrorToast(message: string) {
  showToast(
    {
      text: message,
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
}

interface OTLocation {
  bssid?: string;
  ssid?: string;
  _id?: string;
  acc?: number;
  alt?: number;
  batt?: number;
  bs?: number;
  cog?: number;
  conn?: string;
  created_at?: number;
  lat?: number;
  lon?: number;
  rad?: number;
  t?: string;
  m?: number;
  tid?: string;
  topic?: string;
  tst?: number;
  vac?: number;
  vel?: number;
  p?: number;
  poi?: string;
  tag?: string;
  inregions?: string[];
  inrids?: string[];
}

interface WebSocketMessage {
  command?: WebSocketCommand;
  errorMessage?: string;
  errorDescription?: string;
  locationJson?: string;
}

enum WebSocketCommand {
  ping,
  error,
  subscribeToLocationUpdates,
  locationUpdate,
}
