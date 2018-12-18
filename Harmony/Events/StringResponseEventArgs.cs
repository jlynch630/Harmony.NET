using System;
using System.Collections.Generic;
using System.Text;
using Harmony.Responses;

namespace Harmony.Events {
	/// <summary>
	///		Event arguments for a message received from a Harmony Hub
	/// </summary>
	public class StringResponseEventArgs : EventArgs {
		/// <summary>
		///		Initializes a new instance of the <see cref="StringResponseEventArgs"/> class.
		/// </summary>
		/// <param name="response">The response received from the Hub</param>
		public StringResponseEventArgs(StringResponse response)
		{
			this.Response = response;
		}

		/// <summary>
		///		Gets response received from the Hub
		/// </summary>
		public StringResponse Response { get; }
	}
}
