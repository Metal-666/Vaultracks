# Vaultracks

This project is an alternative to [OwnTracks Recorder](https://github.com/owntracks/recorder), built for my personal use. The main difference between them is that in Vaultracks the location data is stored in an encrypted SQLite database file.

## Limitations

Only the basic features are included:

- HTTP mode only (no MQTT broker)
- Location data only (no transitions, waypoints etc.)
- TLS and Encrypted payloads are not supported
- Basic frontend UI included (see [Web UI](#web-ui))

## Installation

The project can be run using Docker.
Use one of the following commands:

- If you want to store data in a docker volume:

```bash
docker volume create vaultracks-data
docker run --name Vaultracks -v vaultracks-data:/data -p <port_of_your_choice>:8080 metal666/vaultracks:latest
```

- If you want to store data in a folder on your computer:

```bash
docker run --name Vaultracks -v <folder/on/your/computer>:/data -p <port_of_your_choice>:8080 metal666/vaultracks:latest
```

## Usage

- Install Vaultracks using the steps above.
- In the OwnTracks mobile app (assuming you have already installed and set it up to be used with the official OwnTracks Recorder) change the connection mode to HTTP and the URL to `http://<host_ip>:<port_of_your_choice>/api/message`.
- Enter a username in the corresponding field - this will be the name of your database file. It is recommended to only use letters and numbers, as many special symbols cannot be used in file names.
- IMPORTANT: use the Password field to specify the encryption key, and save it somewhere safe. After the first location is reported, your database file will be encrypted using this key.

## The database

Received location data will be written to an SQLite database file. The file name will be `<username>.db`. If you want to change the username, follow these steps:

- Stop location reporting in the mobile app
- Shut down Vaultracks
- Rename the db file
- Change the username in the mobile app
- Restart Vaultracks
- Resume reporting

The database file is encrypted using SQLCipher 4, with default settings. If you want to browse the data directly, use an SQLite database browser with SQLCipher support, for example [DB Browser for SQLite](https://github.com/sqlitebrowser/sqlitebrowser) or [SQLiteStudio](https://github.com/pawelsalawa/sqlitestudio).
The encryption key can be changed following a procedure, similar to changing the username:

- Stop location reporting
- Shut down Vaultracks
- Use a database browser to change the key
- Change the "password" in the mobile app
- Restart Vaultracks
- Resume reporting

The CREATE query for the main table in the database looks like this:

```sql
CREATE TABLE "Location" (
	"Id"	integer NOT NULL,
	"BSSID"	varchar,
	"SSID"	varchar,
	"Accuracy"	integer,
	"Altitude"	integer,
	"Battery"	integer,
	"BatteryStatus"	integer,
	"CourseOverGround"	INTEGER,
	"ConnectionType"	varchar,
	"CreatedAt"	integer,
	"LatitudeString"	varchar,
	"LongitudeString"	varchar,
	"RadiusAroundRegion"	INTEGER,
	"Trigger"	INTEGER,
	"MonitoringMode"	INTEGER,
	"TrackerID"	varchar,
	"Topic"	varchar,
	"Timestamp"	integer,
	"VerticalAccuracy"	integer,
	"Velocity"	integer,
	"Pressure"	float,
	"PointOfInterest"	varchar,
	"Tag"	varchar,
	"InRegionsString"	varchar,
	"InRegionIdsString"	varchar,
	PRIMARY KEY("Id" AUTOINCREMENT)
)
```

Latitude and Longitude are stored as strings, since storing them as float numbers could lead to precision loss. InRegions and InRegionIds are stored as JSON array strings (since arrays are not supported in SQLite).

## Web UI

Similarly to the official Recorder, this project comes with a basic web frontend. You can access it by entering `http://<host_ip>:<port_of_your_choice>` in the address bar of your browser. You will then be prompted to enter the username and the database key, which will be used to open your database file. If the database doesn't exist, it will be created.

![open-database](screenshots/open-database.png?raw=true)
![current-location](screenshots/current-location.png?raw=true)

If you want to display locations reported during a certain period of time, use the Draw Path tool:

![draw-path](screenshots/draw-path.png?raw=true)
![path](screenshots/path.png?raw=true)

## Migration

Existing .rec files (created by OwnTracks Recorder) can be migrated to an SQLite database. For instructions, see [OwnTracksRecUtils](https://github.com/Metal-666/OwnTracksRecUtils).
