using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MallardMessageHandlers.Tests
{
	public class TestData
	{
		public TestData()
		{

		}

		public TestData(string content)
		{
			Content = content;
		}

		public string Content { get; set; }
	}
}
