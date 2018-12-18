// -----------------------------------------------------------------------
// <copyright file="HubConnection.cs" company="John Lynch">
//   This file is licensed under the MIT license.
//   Copyright (c) 2018 John Lynch
// </copyright>
// -----------------------------------------------------------------------

using Harmony.Events;
using Harmony.Responses;

namespace Harmony.WebSockets {
	using System;
	using System.Net.WebSockets;
	using System.Threading;
	using System.Threading.Tasks;

	using CommandParameters;

	using Newtonsoft.Json;

	/// <summary>
	///     A WebSocket connection to a Hub and low-level methods to interface with it
	/// </summary>
	internal class HubConnection : WebSocketConnection {
		/// <summary>
		///     The ID to use when sending messages over the WebSocket
		/// </summary>
		private readonly DeviceID MessageID;

		/// <summary>
		///     Initializes a new instance of the <see cref="HubConnection" /> class.
		/// </summary>
		/// <param name="messageID">The ID to use when sending messages over the WebSocket</param>
		public HubConnection(DeviceID messageID) => this.MessageID = messageID;

		/// <summary>
		///		Event raised when a message has been received on the WebSocket
		/// </summary>
		public event EventHandler<StringResponseEventArgs> OnMessageReceived;

		/// <summary>
		///     Connects to the WebSocket
		/// </summary>
		/// <param name="hostname">The hostname of the Hub, without a port</param>
		/// <param name="hubRemoteId">The RemoteId of the Hub</param>
		/// <returns>When the WebSocket has connected</returns>
		public Task Connect(string hostname, string hubRemoteId) {
			string FullUrl = $"ws://{hostname}:8088/?domain=svcs.myharmony.com&hubId={hubRemoteId}";
			return base.Connect(FullUrl);
		}

		/// <summary>
		///     Handles the websocket connection
		/// </summary>
		/// <returns>When the websocket closes</returns>
		public override async Task HandleWebSocket() {
			try {
				// main listening loop
				while (!this.WebSocket.CloseStatus.HasValue) {
					StringResponse Message = await this.ReceiveHarmonyMessage();

					// pass message on
					this.OnMessageReceived?.Invoke(this, new StringResponseEventArgs(Message));
				}
			}
			catch (WebSocketException) {
				// often other parties fail to close websockets correctly,
				// but avoid throwing an exception if that happens
				// instead, try to reconnect

				// TODO
			}
		}

		/// <summary>
		///     Sends a command with no parameters
		/// </summary>
		/// <param name="commandName">The name of the command to execute</param>
		/// <returns>When the command has been sent</returns>
		public async Task SendCommand(string commandName) => await this.SendCommand(commandName, null);

		/// <summary>
		///     Sends a command with the specified parameters
		/// </summary>
		/// <param name="commandName">The name of the command to execute</param>
		/// <param name="parameters">The parameters to execute the command with</param>
		/// <returns>When the command has been sent</returns>
		public async Task SendCommand(string commandName, CommandParams parameters) {
			Command Command = new Command(commandName, parameters, this.MessageID.Next());
			await this.SendCommand(Command);
		}

		/// <summary>
		///     Sends a command over the WebSocket
		/// </summary>
		/// <param name="command">The command to send</param>
		/// <returns>When the command has been sent</returns>
		private async Task SendCommand(Command command) {
			string CommandJson = JsonConvert.SerializeObject(command);

			// need to surround in another object, using a string is much easier than using JSON.NET
			// nevertheless, TODO make this less weird
			await this.SendMessage("{\"hbus\": " + CommandJson + "}");
		}
	}
}