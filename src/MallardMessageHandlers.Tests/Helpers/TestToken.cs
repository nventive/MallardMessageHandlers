using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MallardMessageHandlers;

namespace MallardMessageHandlers.Tests
{
	public class TestToken : IAuthenticationToken
	{
		public TestToken(string id, bool generateRefreshToken = false)
		{
			Id = id;
			AccessToken = Guid.NewGuid().ToString().Substring(0, 10);
			RefreshToken = generateRefreshToken ? Guid.NewGuid().ToString().Substring(0, 10) : string.Empty;
		}

		public string Id { get; set; }

		public string AccessToken { get; set; }

		public string RefreshToken { get; set; }

		public bool CanBeRefreshed => !string.IsNullOrEmpty(RefreshToken);
	}
}
