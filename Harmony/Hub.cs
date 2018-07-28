// -----------------------------------------------------------------------
// <copyright file="Hub.cs" company="John Lynch">
//   This file is licensed under the MIT license.
//   Copyright (c) 2018 John Lynch
// </copyright>
// -----------------------------------------------------------------------

namespace Harmony {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Net;
	using System.Threading.Tasks;

	using Harmony.CommandParameters;

	using Newtonsoft.Json;

	/// <summary>
	///     A Harmony Hub
	/// </summary>
	[SuppressMessage(
		"ReSharper",
		"CompareOfFloatsByEqualityOperator",
		Justification = "Status codes are statically defined, never determined via math.")]
	public class Hub {
		/// <summary>
		///     The time that the WebSocket connection was initiated
		/// </summary>
		private DateTime ConnectedAt;

		/// <summary>
		///     The function that is currently being "held", or repeatedly pressed, if any
		/// </summary>
		private Function HeldDownFunction;

		/// <summary>
		///     Initializes a new instance of the <see cref="Hub" /> class.
		/// </summary>
		/// <param name="hubInfo">The hub's IP address and name info, usually obtained via discovery</param>
		public Hub(HubInfo hubInfo) => this.Info = hubInfo;

		/// <summary>
		///     Initializes a new instance of the <see cref="Hub" /> class.
		/// </summary>
		/// <param name="ip">The IP address of the hub</param>
		/// <param name="remoteId">The remote id of the hub</param>
		public Hub(string ip, string remoteId) => this.Info = new HubInfo { IP = ip, RemoteId = remoteId };

		/// <summary>
		///     Prevents a default instance of the <see cref="Hub" /> class from being created.
		///     Used for initializing JSON deserialization
		/// </summary>
		[JsonConstructor]
		private Hub() { }

		/// <summary>
		///     An event handler for when an activity has progressed in starting or stopping
		/// </summary>
		public event EventHandler<ActivityProgressEventArgs> ActivityProgress;

		/// <summary>
		///     Gets the list of synced activities on this hub
		/// </summary>
		public Activity[] Activities => this.Sync?.Activities;

		/// <summary>
		///     Gets the list of synced devices on this hub
		/// </summary>
		public Device[] Devices => this.Sync?.Devices;

		/// <summary>
		///     Gets info about the Hub's IP address, name, and more
		/// </summary>
		[JsonProperty]
		public HubInfo Info { get; private set; }

		/// <summary>
		///     Gets or sets the milisecond difference between repeated commands. For example, setting this to 200 would mean
		///     sequential commands executed with a default delay of 400ms would have a 600ms delay, then 200ms, then 600ms, etc.
		/// </summary>
		public int RepeatedCommandAdjustment { get; set; } = 200;

		// We can use a private setter for these because the JsonProperty attribute will make it work anyway

		/// <summary>
		///     Gets info about the Hub's devices, activities, and more
		/// </summary>
		[JsonProperty]
		public HubSync Sync { get; private set; }

		/// <summary>
		///     Gets or sets the Hub's WebSocket connection
		/// </summary>
		private HubConnection Connection { get; set; }

		/// <summary>
		///     Deserializes a JSON string into a <see cref="Hub" />
		/// </summary>
		/// <param name="json">The JSON to deserialize</param>
		/// <returns>The deserialized <see cref="Hub" /></returns>
		public static Hub FromJson(string json) => JsonConvert.DeserializeObject<Hub>(json);

		/// <summary>
		///     Tunes to a channel on the current activity
		/// </summary>
		/// <param name="channel">The channel to tune to</param>
		/// <returns>When the channel has been changed</returns>
		public Task ChangeChannel(Channel channel) => this.ChangeChannel(channel.Number);

		/// <summary>
		///     Tunes to a channel on the current activity
		/// </summary>
		/// <param name="channelNumber">The number of the channel to tune to</param>
		/// <returns>When the channel has been changed</returns>
		public async Task ChangeChannel(string channelNumber) {
			ChangeChannelParams Params = new ChangeChannelParams(channelNumber, this.GetTimestamp());
			Response<dynamic> Response =
				await this.Connection.SendCommand<dynamic>("harmony.engine?changeChannel", Params);

			if (Response.Code != 200) throw new HarmonyWebSocketException(Response.Code, Response.Message);
		}

		/// <summary>
		///     Connects to the Hub asynchronously
		/// </summary>
		/// <param name="deviceID">A unique identifier for the device connecting</param>
		/// <returns>When the WebSocket has connected</returns>
		public async Task ConnectAsync(DeviceID deviceID) {
			this.Connection = new HubConnection(deviceID);
			await this.Connection.Connect(this.Info.IP, this.Info.RemoteId);
			this.ConnectedAt = DateTime.Now;
		}

		/// <summary>
		///     Disconnects from the Hub asynchronously
		/// </summary>
		/// <returns>When the WebSocket has disconnected</returns>
		public Task Disconnect() => this.Connection.Disconnect();

		/// <summary>
		///     Ends the currently running activity
		/// </summary>
		/// <returns>When the activity has ended</returns>
		public async Task EndActivity() =>
			await this.RunActivity(
				await this.GetRunningActivity() ?? throw new HarmonyException("There is no running activity to end"),
				false);

		/// <summary>
		///     Executes a custom sequence
		/// </summary>
		/// <param name="sequence">The sequence to execute</param>
		/// <returns>When the sequence has executed</returns>
		public Task FireSequenceAsync(Sequence sequence) => this.FireSequenceAsync(sequence.ID);

		/// <summary>
		///     Executes a custom sequence
		/// </summary>
		/// <param name="sequenceId">The id of the sequence to execute</param>
		/// <returns>When the sequence has executed</returns>
		public Task FireSequenceAsync(string sequenceId) =>
			this.HoldActionAsync($"{{\"sequenceId\": {sequenceId}}}", "press");

		/// <summary>
		///     Gets an activity by its id
		/// </summary>
		/// <param name="activityId">The id of the activity to get</param>
		/// <returns>The activity, or null if none with the provided id exists</returns>
		public Activity GetActivityById(string activityId) =>
			this.Sync.Activities.FirstOrDefault(x => x.Id == activityId);

		/// <summary>
		///     Gets an activity by its label
		/// </summary>
		/// <param name="activityLabel">The label of the activity to get</param>
		/// <returns>The activity, or null if none with the provided label exists</returns>
		public Activity GetActivityByLabel(string activityLabel) =>
			this.Sync.Activities.FirstOrDefault(x => x.Label == activityLabel);

		/// <summary>
		///     Gets a device by its id
		/// </summary>
		/// <param name="deviceId">The id of the device to find</param>
		/// <returns>The device with a matching id, or null if none exists</returns>
		public Device GetDeviceById(string deviceId) => this.Sync?.Devices.FirstOrDefault(x => x.ID == deviceId);

		/// <summary>
		///     Gets a device by its model name
		/// </summary>
		/// <param name="model">The model of the device to find</param>
		/// <returns>The device with the matching model, or null if there is none</returns>
		public Device GetDeviceByModel(string model) => this.Sync.Devices.FirstOrDefault(x => x.Model == model);

		/// <summary>
		///     Gets a device by both its model and manufacturer
		/// </summary>
		/// <param name="model">The model name of the device to find</param>
		/// <param name="manufacturer">The manufacturer of the device to find</param>
		/// <returns>The device with the matching model and maufacturer, or null if there is none</returns>
		public Device GetDeviceByModel(string model, string manufacturer) =>
			this.Sync.Devices.FirstOrDefault(x => x.Model == model && x.Manufacturer == manufacturer);

		/// <summary>
		///     Gets the channels for a specific provider
		/// </summary>
		/// <param name="providerID">The television provider's ID</param>
		/// <returns>The <see cref="ProviderInfo" /></returns>
		public async Task<ProviderInfo> GetInfoForProvider(string providerID) {
			string RequestUrl = this.Sync.Content.ContentServiceHost.Replace("{providerId}", providerID);

			return await Hub.GetJSONData<ProviderInfo>(RequestUrl);
		}

		/// <summary>
		///     Gets the currently running activity
		/// </summary>
		/// <returns>The running activity, or null if there is none running</returns>
		public async Task<Activity> GetRunningActivity() {
			string RunningActivityId = await this.GetRunningActivityId();
			return RunningActivityId != "-1" ? this.GetActivityById(RunningActivityId) : null;
		}

		/// <summary>
		///     Gets the id of the currently running activity
		/// </summary>
		/// <returns>The id of the currently running activity, or "-1" if there is none</returns>
		public async Task<string> GetRunningActivityId() => (await this.GetStateAsync()).ActivityId;

		/// <summary>
		///     Updates the current state of the Hub (running activity, activity status, etc.)
		/// </summary>
		/// <returns>The updated state has been updated</returns>
		public async Task<StateDigest> GetStateAsync() {
			Response<StateDigest> Response = await this.Connection.SendCommand<StateDigest>(
				                                 "connect.statedigest?get",
				                                 new FormatParams());

			// anything between 200 and 300 indicates success
			return Response.Code >= 200 && Response.Code < 300
				       ? Response.Data
				       : throw new HarmonyWebSocketException(Response.Code, Response.Message);
		}

		/// <summary>
		///     Gets the user's location, favorite channels, and id from the server
		/// </summary>
		/// <returns>The retrieved <see cref="User" /></returns>
		public async Task<User> GetUserInfo() {
			string RequestUrl = this.Sync.Content.ContentUserHost.Replace(
				"{userProfileUri}",
				Uri.EscapeDataString(this.Sync.Content.HouseholdUserProfileUri));

			return await Hub.GetJSONData<User>(RequestUrl);
		}

		/// <summary>
		///     Presses a button on a device, e.g. hitting play
		/// </summary>
		/// <param name="function">The function to execute</param>
		/// <returns>When the function has executed</returns>
		public Task PressButtonAsync(Function function) => this.HoldActionAsync(function, "press");

		/// <summary>
		///     Presses multiple buttons on the device, one after the other.
		///     Minimum delay of 400ms
		/// </summary>
		/// <param name="delay">
		///     The delay between button presses. This will be altered by +/- 200 miliseconds every command:
		///     otherwise Harmony won't accept the commands
		/// </param>
		/// <param name="functions">The functions to execute</param>
		/// <returns>When all functions have executed</returns>
		public async Task PressButtonsAsync(int delay, IEnumerable<Function> functions) {
			if (delay < 400) delay = 400;

			// I still don't quite understand this, but ReSharper says something about a "possible multiple enumeration of IEnumerable"
			Function[] Enumerable = functions as Function[] ?? functions.ToArray();

			if (!Enumerable.Any()) return;

			// call the first function immediately
			await this.PressButtonAsync(Enumerable.First());

			// harmony will not accept the command if you send them at too regular intervals. Use an alternating
			int Adjustment = this.RepeatedCommandAdjustment;
			foreach (Function Function in Enumerable.Skip(1)) {
				await Task.Delay(delay + Adjustment);
				await this.PressButtonAsync(Function);
				Adjustment = -Adjustment;
			}
		}

		/// <summary>
		///     Presses multiple buttons on the device, one after the other.
		///     Minimum delay of 400ms
		/// </summary>
		/// <param name="delay">
		///     The delay between button presses. This will be altered by +/- 200 miliseconds every command:
		///     otherwise Harmony won't accept the commands
		/// </param>
		/// <param name="functions">The functions to execute</param>
		/// <returns>When all functions have executed</returns>
		public Task PressButtonsAsync(int delay, params Function[] functions) =>
			this.PressButtonsAsync(delay, (IEnumerable<Function>)functions);

		/// <summary>
		///     Sends a <see cref="FixActivityAction" /> over the websocket
		/// </summary>
		/// <param name="action">The action to execute to fix the activity</param>
		/// <returns>When the action has been sent</returns>
		public async Task SendFixActivityAction(FixActivityAction action) {
			await this.Connection.SendCommand(
				"vnd.logitech.harmony/vnd.logitech.harmony.engine?helpSync",
				action.GetParameters(this.GetTimestamp()));
		}

		/// <summary>
		///     Starts an activity
		/// </summary>
		/// <param name="activity">The activity to start</param>
		/// <returns>When the activity has been completely started</returns>
		public Task StartActivity(Activity activity) => this.RunActivity(activity, true);

		/// <summary>
		///     Starts holding down the given function until <code>StopHolding</code> is called
		/// </summary>
		/// <param name="function">The function to hold down</param>
		public void StartHolding(Function function) {
			this.HeldDownFunction = function;
			this.HoldFunction();
		}

		/// <summary>
		///     Stops holding down the currently held down function
		/// </summary>
		public void StopHolding() {
			this.HeldDownFunction = null;
		}

		/// <summary>
		///     Syncs the hub's devices, activities, etc. asynchronously
		/// </summary>
		/// <returns>When the hub has synced</returns>
		public async Task SyncConfigurationAsync() {
			Response<HubSync> Response =
				await this.Connection.SendCommand<HubSync>("vnd.logitech.harmony/vnd.logitech.harmony.engine?config");

			// anything between 200 and 300 indicates success
			this.Sync = Response.Code >= 200 && Response.Code < 300
				            ? Response.Data
				            : throw new HarmonyWebSocketException(Response.Code, Response.Message);
		}

		/// <summary>
		///     Serializes the <see cref="Hub" /> into a JSON string
		/// </summary>
		/// <returns>The serialized JSON <see cref="Hub" /></returns>
		public string ToJson() => JsonConvert.SerializeObject(this);

		/// <summary>
		///     Gets JSON data from a url
		/// </summary>
		/// <typeparam name="T">The type to deserialize the JSON as</typeparam>
		/// <param name="requestUrl">The URL to request</param>
		/// <returns>The deserialized <typeparamref name="T" /></returns>
		private static async Task<T> GetJSONData<T>(string requestUrl) {
			WebClient Client = new WebClient();
			Client.Headers.Add(HttpRequestHeader.AcceptLanguage, "en-US,en;q=0.8");
			Client.Headers.Add(
				"Logitech-API-Key",
				"f206270e-26aa-4927-8630-fd230be3fbb7"); // value is hardcoded in the app
			return JsonConvert.DeserializeObject<T>(await Client.DownloadStringTaskAsync(requestUrl));
		}

		/// <summary>
		///     Tells the Hub to continue holding down the currently held down function
		/// </summary>
		/// <returns>When the command has executed</returns>
		private Task ContinueHoldingAsync() => this.HoldActionAsync(this.HeldDownFunction, "hold");

		/// <summary>
		///     Gets the current timestamp for HoldAction
		/// </summary>
		/// <returns>The time since the WebSocket connection was made, in ms</returns>
		private long GetTimestamp() => (DateTime.Now - this.ConnectedAt).Milliseconds;

		/// <summary>
		///     Executes a holdAction command on the hub
		/// </summary>
		/// <param name="function">The function to execute</param>
		/// <param name="status">The status, either press or hold</param>
		/// <returns>When the function has executed</returns>
		private Task HoldActionAsync(Function function, string status) =>
			this.HoldActionAsync(function?.Action, status);

		/// <summary>
		///     Executes a holdAction command on the hub
		/// </summary>
		/// <param name="action">The JSON of the action to execute</param>
		/// <param name="status">The status, either press or hold</param>
		/// <returns>When the function has executed</returns>
		private async Task HoldActionAsync(string action, string status) {
			if (action == null) return;

			HoldActionParams Parameters =
				new HoldActionParams { Action = action, Status = status, Timestamp = this.GetTimestamp() };

			await this.Connection.SendCommand(
				"vnd.logitech.harmony/vnd.logitech.harmony.engine?holdAction",
				Parameters);
		}

		/// <summary>
		///     Holds down the currently held function until it becomes null
		/// </summary>
		private async void HoldFunction() {
			while (this.HeldDownFunction != null) {
				await Task.Delay(200);
				await this.ContinueHoldingAsync();
			}
		}

		/// <summary>
		///     Starts or stops an activity
		/// </summary>
		/// <param name="activity">The activity to run</param>
		/// <param name="start">True to start the activity, false to end it</param>
		/// <returns>When the activity has completely started or stopped</returns>
		private async Task RunActivity(Activity activity, bool start) {
			if (activity == null)
				throw new HarmonyException("Cannot run a null activity");

			RunActivityParams Parameters = new RunActivityParams(activity.Id, this.GetTimestamp().ToString(), start);

			// run without expecting a response. We'll collect those manually
			await this.Connection.SendCommand("harmony.activityengine?runactivity", Parameters);

			// we will get four things here:
			/*
			 * 1. harmony.activityengine?runactivity, noting that the command was received
			 * 2. harmony.engine?startActivity (code 200.2), noting the current home automation state (we don't care about this one)
			 * 3. harmony.engine?startActivity (code 100), indicating the progress of starting the activity
			 * 4. harmony.engine?startActivityFinished, indicating that starting has finished
			 *
			 * We don't really care about the first two, but the last two will allow us to notify progress and completion
			 */
			StringResponse Message;

			// while the activity hasn't yet finished starting
			while ((Message = await this.Connection.ReceiveMessage()).Command
			       != "harmony.engine?startActivityFinished") {
				// both of these message commands indicate progress
				if (Message.Command != "harmony.engine?startActivity"
				    && Message.Command != "harmony.engine?helpdiscretes") continue;
				if (Message.Code == 200) break; // 200 indicates completion
				if (Message.Code != 100) continue; // 100 progress

				ActivityProgressResponseData ProgressData = Message.DeserializeAs<ActivityProgressResponseData>();
				double Progress = (double)ProgressData.Done / ProgressData.Total;

				// invoke event handler
				this.ActivityProgress?.Invoke(this, new ActivityProgressEventArgs(activity, Progress));
			}
		}
	}

	/// <inheritdoc />
	/// <summary>
	///     Event arguments for activity start or stop progress
	/// </summary>
	public class ActivityProgressEventArgs : EventArgs {
		/// <inheritdoc />
		/// <summary>
		///     Initializes a new instance of the <see cref="ActivityProgressEventArgs" /> class.
		/// </summary>
		/// <param name="activity">The activity that is starting or stopping</param>
		/// <param name="progress">The activity's starting or stopping progress</param>
		public ActivityProgressEventArgs(Activity activity, double progress) {
			this.Activity = activity;
			this.Progress = progress;
		}

		/// <summary>
		///     Gets the activity that is starting or stopping
		/// </summary>
		public Activity Activity { get; }

		/// <summary>
		///     Gets the activity's starting or stopping progress
		/// </summary>
		public double Progress { get; }
	}
}