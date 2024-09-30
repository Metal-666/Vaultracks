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

const locationUpdated = document.getElementById(
  "location-updated",
) as HTMLElement;

const dialogBarrier = document.getElementById(
  "dialog-barrier",
) as HTMLElement;
const dialogWrapper = document.getElementById(
  "dialog-wrapper",
) as HTMLElement;

const openDatabaseDialogTemplate = document.getElementById(
  "open-database-dialog-template",
) as HTMLTemplateElement;
const drawPathDialogTemplate = document.getElementById(
  "draw-path-dialog-template",
) as HTMLTemplateElement;

const pathPointPopupContentTemplate = document.getElementById(
  "path-point-popup-content-template",
) as HTMLTemplateElement;

const dialogTemplates = {
  openDatabase: openDatabaseDialogTemplate,
  drawPath: drawPathDialogTemplate,
};

const map = L.map("map").setView([51.505, -0.09], 13);

map.zoomControl.setPosition("bottomright");

L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
  maxZoom: 19,
  attribution:
    '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
}).addTo(map);

let marker: L.Marker | undefined;

function mapZoomChanged() {
  map.setZoom(mapZoomSlider.valueAsNumber);
}

let webSocket: WebSocket | undefined;

async function openDatabase() {
  const openDatabaseForm = document.forms.namedItem("open-database");

  console.log("Opening db...");

  if (openDatabaseForm == null) {
    console.error("Failed to open db: credentials form does not exist!");

    return;
  }

  const usernameInput = openDatabaseForm.elements.namedItem(
    "username",
  ) as HTMLInputElement;
  const dbKeyInput = openDatabaseForm.elements.namedItem(
    "password",
  ) as HTMLInputElement;

  webSocket = new WebSocket(
    `/ws?username=${encodeURIComponent(usernameInput.value)}&databaseKey=${
      encodeURIComponent(dbKeyInput.value)
    }`,
  );

  let keepAliveInterval: number | undefined;

  webSocket.addEventListener("open", () => {
    console.log("WS connection established!");

    setDialog();

    keepAliveInterval = setInterval(() => {
      sendWsMessage({
        command: WebSocketCommand.ping,
      });
    }, 15 * 1000);

    sendWsMessage({
      command: WebSocketCommand.subscribeToLocationUpdates,
    });
  });

  webSocket.addEventListener("close", (event) => {
    console.warn("WS connection closed!");

    showErrorToast(
      `WebSocket connection failed or was terminated! Reason:\n${
        event.reason ? event.reason : "Possibly an internal error..."
      }`,
    );

    setDialog(dialogTemplates.openDatabase);

    if (keepAliveInterval == null) {
      return;
    }

    clearInterval(keepAliveInterval);
  });

  webSocket.addEventListener("message", (event: MessageEvent<string>) => {
    console.debug(`Received message: `, event.data);

    const message = JSON.parse(event.data) as WebSocketMessage;

    if (message.command == null) {
      sendWsError("Command was not provided!");

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
            "Received message with missing property!",
            "(locationJson)",
          );

          break;
        }

        const location = JSON.parse(message.locationJson) as OTLocation;

        if (location.lat == null || location.lon == null) {
          sendWsError("Latitude and/or longitude missing from payload!");

          break;
        }

        const latLng: L.LatLngExpression = [location.lat, location.lon];

        if (marker == null) {
          marker = L.marker(latLng).addTo(map);
        } else {
          marker.setLatLng(latLng);
        }

        const createdAtWithTimestampFallback = location.created_at ??
          location.tst;

        currentCoordinates.value = `${location.lat}, ${location.lon}`;
        currentTimestamp.value = createdAtWithTimestampFallback == null
          ? "unknown"
          : new Date(createdAtWithTimestampFallback * 1000)
            .toLocaleString();

        if (followMarkerCheckbox.checked) {
          map.flyTo(latLng, mapZoomSlider.valueAsNumber);
        }

        locationUpdated.textContent = `Location updated at ${
          (new Date()).toLocaleTimeString()
        }`;

        break;
      }
      case WebSocketCommand.requestedLocation: {
        if (message.locationJson == null) {
          sendWsError(
            "Received message with missing property!",
            "(locationJson)",
          );

          break;
        }

        const location = JSON.parse(message.locationJson) as OTLocation;

        if (location.lat == null || location.lon == null) {
          sendWsError("Latitude and/or longitude missing from payload!");

          showErrorToast("Failed to add point: coordinates missing!");

          break;
        }

        const createdAtWithTimestampFallback = location.created_at ??
          location.tst;

        if (createdAtWithTimestampFallback == null) {
          sendWsError("Timestamp missing from payload!");

          showErrorToast("Failed to add point: timestamp missing!");

          break;
        }

        const latLng: L.LatLngExpression = [location.lat, location.lon];

        const circle = L.circleMarker(latLng, {
          radius: 6,
          fillOpacity: 1,
          className: "stroke-mauve fill-mauve/50",
        }).addTo(map);

        const popupContent = (pathPointPopupContentTemplate.content.cloneNode(
          true,
        ) as DocumentFragment).firstElementChild as HTMLElement;

        ((popupContent.getElementsByClassName(
          "popup-coordinates",
        )).item(0) as HTMLInputElement).value =
          `${location.lat}, ${location.lon}`;
        (popupContent.getElementsByClassName(
          "popup-timestamp",
        ).item(0) as HTMLInputElement).value = new Date(
          createdAtWithTimestampFallback * 1000,
        )
          .toLocaleString();

        const popup = L.popup({
          content: popupContent,
          maxWidth: 500,
          className: "custom-leaflet-popup",
        });

        circle.bindPopup(popup);

        break;
      }
    }
  });
}

function drawPath() {
  const drawPathForm = document.forms.namedItem("draw-path");

  console.log("Drawing path...");

  if (drawPathForm == null) {
    console.error("Failed to draw path: time range form does not exist!");

    return;
  }

  const fromCheckbox = drawPathForm.elements.namedItem(
    "datetime-from-checkbox",
  ) as HTMLInputElement;
  const toCheckbox = drawPathForm.elements.namedItem(
    "datetime-to-checkbox",
  ) as HTMLInputElement;

  const fromInput = drawPathForm.elements.namedItem(
    "datetime-from",
  ) as HTMLInputElement;
  const toInput = drawPathForm.elements.namedItem(
    "datetime-to",
  ) as HTMLInputElement;

  const fromTimestamp = fromInput.valueAsNumber;
  const toTimestamp = toInput.valueAsNumber;

  let fromDate = Number.isNaN(fromTimestamp) ? null : new Date(fromTimestamp);
  let toDate = Number.isNaN(toTimestamp) ? null : new Date(toTimestamp);

  if (fromDate) {
    fromDate = new Date(fromDate.toISOString().slice(0, -5));
  }

  if (toDate) {
    toDate = new Date(toDate.toISOString().slice(0, -5));
  }

  console.log(`Time frame: ${fromDate} -> ${toDate}`);

  if (fromCheckbox.checked && !fromDate) {
    showErrorToast("Please enter the 'from' date & time!", 5 * 1000);

    return;
  }

  if (toCheckbox.checked && !toDate) {
    showErrorToast("Please enter the 'to' date & time!", 5 * 1000);

    return;
  }

  if (
    (fromDate && toDate) &&
    (fromDate > toDate)
  ) {
    showErrorToast("Invalid time frame (from > to)!", 5 * 1000);

    return;
  }

  setDialog();

  sendWsMessage({
    command: WebSocketCommand.requestLocations,
    fromTimestamp: !fromDate ? undefined : fromDate.getTime() / 1000,
    toTimestamp: !toDate ? undefined : toDate.getTime() / 1000,
  });
}

function sendWsMessage(
  webSocketMessage: WebSocketMessage,
) {
  console.debug("Sending WS message...");

  if (!webSocket) {
    console.error(
      "Failed to send WS message: WS connection hasn't been established!",
    );

    return;
  }

  if (webSocket.readyState !== webSocket.OPEN) {
    console.error(
      "Failed to send WS message: WS connection is closed!",
    );

    return;
  }

  const jsonData = JSON.stringify(webSocketMessage);

  console.debug(`Data:`, jsonData);

  webSocket.send(jsonData);
}

function sendWsError(
  message: string,
  description?: string,
) {
  sendWsMessage({
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

function showErrorToast(message: string, duration = -1) {
  showToast(
    {
      text: message,
      duration: duration,
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

let currentDialog:
  | { element: HTMLElement; template: HTMLTemplateElement }
  | undefined;

let currentDialogAnimation: anime.AnimeTimelineInstance | undefined;

function setDialog(dialogTemplate?: HTMLTemplateElement) {
  console.log("Setting dialog: ", dialogTemplate);
  console.log("Current dialog: ", currentDialog);

  if (dialogTemplate == null) {
    if (currentDialog != null) {
      cancelAnimation(currentDialogAnimation);

      currentDialogAnimation = anime.timeline({
        duration: 300,
        easing: "easeInOutQuint",
        complete: () => {
          dialogBarrier.style.display = "none";

          currentDialog?.element.remove();
          currentDialog = undefined;
        },
      }).add({
        targets: currentDialog.element,
        scale: 0,
      }, 0).add({
        targets: dialogBarrier,
        opacity: 0,
      }, 0);
    }

    return;
  }

  currentDialog?.element.remove();

  const dialogFragment = dialogTemplate.content.cloneNode(
    true,
  ) as DocumentFragment;

  dialogWrapper.appendChild(dialogFragment);

  const dialog = dialogWrapper.firstElementChild as HTMLElement;

  currentDialog = {
    element: dialog,
    template: dialogTemplate,
  };

  cancelAnimation(currentDialogAnimation);

  currentDialogAnimation = anime.timeline({
    duration: 300,
    easing: "easeInOutCirc",
    begin: () => {
      dialogBarrier.style.display = "inherit";
    },
  }).add({
    targets: dialog,
    scale: 1,
  }, 0).add({
    targets: dialogBarrier,
    opacity: 1,
  }, 0);
}

setDialog(dialogTemplates.openDatabase);

function cancelAnimation(animation: anime.AnimeInstance | undefined) {
  if (!animation) {
    return;
  }

  anime.running.splice(anime.running.indexOf(animation), 1);
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
  fromTimestamp?: number;
  toTimestamp?: number;
}

enum WebSocketCommand {
  ping,
  error,
  subscribeToLocationUpdates,
  locationUpdate,
  requestLocations,
  requestedLocation,
}
