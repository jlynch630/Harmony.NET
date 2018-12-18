using System;
using System.Collections.Generic;
using System.Text;
using Harmony.Responses;

namespace Harmony.Events {
	/// <summary>
	///		Event arguments for a Harmony WebSocket message
	/// </summary>
	/// <typeparam name="TData">The type of data received in this message</typeparam>
	public class HarmonyEventArgs<TData> : EventArgs {
		/// <summary>
		///		Initializes a new instance of the <see cref="HarmonyEventArgs{TData}"/> class.
		/// </summary>
		/// <param name="data">The data received in the message</param>
		public HarmonyEventArgs(TData data) {
			this.Data = data;
		}

		/// <summary>
		///		Gets the data received in the message
		/// </summary>
		public TData Data { get; }
	}
}
