Harmony.NET
===========

Harmony.NET is an API that enables local-network websocket communication with Logitech Harmony Hubs. Harmony.NET makes it easy to start and stop activities, press remote buttons, and more in .NET.
Compatible with both .NET Core and the Windows .NET Framework.

To quickly get started, [install](#installation) the package and head down to the [quickstart](#quickstart) below.

Installation
------------
The easiest way to install Harmony.NET is via [NuGet](https://www.nuget.org/packages/Harmony.NET/):

```
Install-Package Harmony.NET
```

To build from source, clone the repository and build **HarmonyHub.sln** in Visual Studio.
The output files will be located in `\HarmonyHub\HarmonyHub\bin\(Debug/Release)\netstandard2.0\Harmony.dll`

Usage
-----
Start by importing the library at the top of your file:

```csharp
using Harmony;
```

From there, the first step is to get a reference to a `Hub`. This can be done in three ways

1. **Using a `DiscoveryService`**

	This is the best method to use if you don't know the Hub's IP address beforehand.
	A `DiscoveryService` broadcasts a probe over UDP every 5 seconds by default to discover Hubs on the network.

	First, define a new instance of the `DiscoveryService`:

	```csharp
	DiscoveryService Service = new DiscoveryService();
	```

	Then, subscribe to the event listener `HubFound` on the service, which is called every time a new Hub is found:

	```csharp
	Service.HubFound += Service_HubFound;
	```

	Finally, start discovery:

	```csharp
	Service.StartDiscovery();
	```

	The event listener will be passed `HubFoundEventArgs`, which contains a `HubInfo` property which can be used to create a `Hub`

	```csharp
	private static async void HubFound(object sender, HubFoundEventArgs e) {
				Hub Hub = new Hub(e.HubInfo);
				. . .
	}
	```

2. **Using the Hub's IP Address and ID**

	Another way to create a Hub is by using its IP Address and Remote ID.

	These properties can be retrieved from an existing Hub via:

	```csharp
	string IP = Hub.Info.IP
	string RemoteID = Hub.Info.RemoteId;
	```
	Or HubInfo via:

	```csharp
	string IP = HubInfo.IP
	string RemoteID = HubInfo.RemoteId;
	``` 

	To create the Hub, use the constructor:

	```csharp
	Hub Hub = new Hub(IP, RemoteID);
	```

3. **Saving and restoring the Hub with JSON**

	You can also save the Hub's state to JSON:

	```csharp
	string Json = Hub.ToJson();
	```
	Then, restore the state using that JSON:

	```csharp
	Hub Hub = Hub.FromJson(Json);
	```

Device IDs
----------
Harmony requires that each client provide a unique ID to communicate with it. The easiest way to get one of these is like so:

```csharp
DeviceID ClientID = DeviceID.GetDeviceDefault();
```

However, it is also possible to create a completely custom Device ID just by using its constructor:

```csharp
DeviceID ClientID = new DeviceID(
	deviceUUID: Guid.NewGuid().ToString("N"),
	deviceProductName: "lg-joan",
	deviceModelName: "lge-h8930"
);
```

Using the Hub
-------------
The first step after obtaining a `Hub` instance is to connect to it using the `DeviceID` from earlier:

```csharp
await Hub.ConnectAsync(ClientID)
```

**Note**: `ConnectAsync`, like most methods in Harmony.NET, is asynchronous. This means you should probably `await` it in an `async` function for best performance.

Next, you'll want to retrieve the list of devices and activities on this hub, which can be done using `SyncConfigurationAsync`:

```csharp
await Hub.SyncConfigurationAsync();
```

**Note**: `SyncConfigurationAsync` does not return anything, it only updates the Hub itself. To access the raw data, use the `Hub.Sync` property. It is also unnecessary to call this often. Once every app launch should do.

### Controlling individual devices
1. **Getting a device**

	There are a few ways to grab an individual `Device`.

	The first is to look up a device by its model:

	```csharp
	Device Xbox = Hub.GetDeviceByModel("Xbox One", "Microsoft");

	// including the manufacturer is not always necessary
	Device Xbox = Hub.GetDeviceByModel("Xbox One");
	```

	The second is to use a known device id:

	```csharp
	// getting the id
	string ID = MyDevice.ID;
	. . .
	// using the id
	Device MyDevice = Hub.GetDeviceById(ID);
	```

	Finally, you can also use the `Devices` array on a `Hub` and any LINQ expressions to find exactly what you are looking for.

	```csharp
	Device Newest = Hub.Devices.OrderByDescending(device => device.AddedDate).First();
	```

2. **Controlling a device**

	Devices are controlled via functions, akin to buttons on remotes. These `Function`s can be passed into various methods in the `Hub` to execute them:

	```csharp
	Function MyFunction = . . .; // see below for details on getting functions
	await Hub.PressButtonAsync(MyFunction);

	// hold button for two seconds
	Hub.StartHolding(MyFunction);
	await Task.Delay(2000);
	Hub.StopHolding();
	```

### Starting and stopping activities

While all activities are in the `Activities` array on a `Hub`, the easiest way to get a reference to one is by using its label or id:

```csharp
Activity WatchTV = Hub.GetActivityById("12345678");
Activity WatchTV = Hub.GetActivityByLabel("Watch TV");
```

From there, starting and stopping the activity is trivial:

```csharp
await Hub.StartActivity(WatchTV);
. . .
await Hub.StopActivity(); // stops the currently running activity, throws if there isn't one
```

Optionally, if you want to track the progress of stopping or stopping an activity, subscribe to `Hub.ActivityProgress`:

```csharp
Hub.ActivityProgress += (sender, e) => {
	// e.progress is a double between 0 and 1
	Console.WriteLine("Starting activity. {0}% done.", Math.Floor(e.Progress * 100));
};
```

To control an activity, see the section below on retrieving functions

### Retrieving functions

The easiest way to get a function is by using a `Device Wrapper`. A device wrapper provides easy access to commonly used functions.

**Note**: Despite the name, device wrappers can be used with both activities and devices.

There are currently three wrappers:

* The simplest one, `DeviceWrapper` provides power on, off, and toggle power.

* `NumericNavigationWrapper`, which inherits from `DeviceWrapper`, provides the number pad and navigation functions like `up`, `down`, `select`, etc.

* `PlaybackWrapper`, which inherits from `NumericNavigationWrapper`, provides functions like `play`, `skip`, etc.

See the examples below for how to use a `PlaybackWrapper`:

```csharp
using Harmony.DeviceWrappers;
. . .
PlaybackWrapper DVDWrapper = new PlaybackWrapper(DVDPlayerDevice);
await Hub.PressButtonAsync(DVDWrapper.Play);
```

```csharp
PlaybackWrapper PlaybackControls = new PlaybackWrapper(WatchTVActivity); // you can also control activities with wrappers
await Harmony.PressButtonAsync(PlaybackControls.Left);
```

Alternatively, for functions not available on a wrapper, a function can be retrieved using the `GetFunctionByName` method on a `Controllable` object (`Device` or `Activity`):

```csharp
await Hub.PressButtonAsync(DVDPlayerDevice.GetFunctionByName("TopMenu"));

await Hub.PressButtonAsync(WatchTVActivity.GetFunctionByName("Guide"));
```

Finally, all `Functions` are available on `Controllables` either as one array or sorted into `ControlGroups`

```csharp
Function Play = Device.GetControlGroupByName("TransportBasic").GetFunctionByName("Play");

// not recommended as these can be unreliable
Function Play = Device.Functions[4]; 
Function Play = Device.ControlGroups[5].Functions[1];
```

### TV Providers

Harmony supports getting information about the user's TV providers and their channels.

First, you'll need to download information about the user. This includes the user's location, and TV provider and favorite channels for particular devices.

```csharp
User User = await Hub.GetUserInfo();
```

Next, get the TV providers for a specific device, say a DirecTV DVR, and download the main provider's channels

```csharp
// Get channels on DVR TV provider
ProviderList DirecTVProviders = User.Preferences.GetProvidersForDevice(DirecTV);

// There's usually only one provider per device, so just get the default
ProviderInfo ProviderInfo =	await Hub.GetInfoForProvider(DirecTVProviders.DefaultProviderId);
```

Once you have the provider's channels, you can access data like channel numbers, names, and popularity. For example, here's how to tune to the most popular channel:

```csharp
Channel MostPopular = ProviderInfo.PopularChannels.First();
await Hub.ChangeChannel(MostPopular);
Console.WriteLine("Tuned to channel {0}", MostPopular.Station.FullName);
```

Or to tune to ESPN:

```csharp
Channel ESPN = ProviderInfo.GetChannelByStationShortName("ESPN");
await Hub.ChangeChannel(ESPN);
```

Or a favorite channel:

```csharp
string FavoriteChannelId = DirecTVProviders.DefaultFavoriteChannels.StationIDs.First();
Channel Favorite = ProviderInfo.GetChannelByStationID(FavoriteChannelId);
await Hub.ChangeChannel(Favorite);
```

Note that if you know the channel number in advance, you don't have to go through all of the steps above to tune to it.

```csharp
await Harmony.ChangeChannel("206");
```

Sending Multiple Functions in a Row
===================================

Harmony has an issue (or perhaps a feature) that prevents sending commands at regular intervals. For example, the third command will not be executed in the code below:

```csharp
// broken
await Hub.PressButtonAsync(Wrapper.Up);
await Task.Delay(400);
await Hub.PressButtonAsync(Wrapper.Right);
await Task.Delay(400);
await Hub.PressButtonAsync(Wrapper.Select);
```

To fix this issue, `Hub` has a method `PressButtonsAsync` (note the "s") which allows sending multiple commands at a semi-regular interval:

```csharp
// takes either a params Function array
await Hub.PressButtonsAsync(400, Wrapper.Number0, Wrapper.Number1, Wrapper.Number2);

// or an IEnumerable<Function>
await Hub.PressButtonsAsync(400, WatchTV.Functions.Where(fn => fn.Name.StartsWith("Number")));
```

The example above is equivalent to the following code (400ms difference between each call):

```csharp
// works!
await Hub.PressButtonAsync(Wrapper.Number0);
await Task.Delay(600);
await Hub.PressButtonAsync(Wrapper.Number1);
await Task.Delay(200);
await Hub.PressButtonAsync(Wrapper.Number2);
```

This +/- 200ms adjustment can be changed by setting `Hub.RepeatedCommandAdjustment` to any ms value. Setting it to 0 will disable this feature.

Quickstart
==========
This quickstart demonstrates discovering a hub, starting an activity, and tuning to a channel
```csharp
DiscoveryService Service = new DiscoveryService();
Service.HubFound += async (sender, e) => {
	// stop discovery once we've found one hub
	Service.StopDiscovery();

	Hub Harmony = new Hub(e.HubInfo);
	await Harmony.ConnectAsync(DeviceID.GetDeviceDefault());
	await Harmony.SyncConfigurationAsync();

	// start activity watch tv
	Activity WatchTV = Harmony.GetActivityByLabel("Watch TV");
	await Harmony.StartActivity(WatchTV);

	// tune to ESPN
	await Harmony.ChangeChannel("206");
	PlaybackWrapper PlaybackControls = new PlaybackWrapper(WatchTV);

	// wait for channel to switch, and pause
	await Task.Delay(1000);
	await Harmony.PressButtonAsync(PlaybackControls.Pause);
};
Service.StartDiscovery();
```

Issues and Suggestions
======================
If you have any problems or features you'd like to see me add to Harmony.NET, I'd be happy to hear about them. Please make a [new issue](https://github.com/jlynch630/Harmony.NET/issues/new)

License
=======
This project is licensed under the MIT License.
See more information in the [LICENSE](https://github.com/jlynch630/Harmony.NET/blob/master/LICENSE) file.

Copyright
=========
Copyright 2018 (c) John Lynch