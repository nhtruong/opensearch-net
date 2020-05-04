// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

﻿using System.Threading.Tasks;
using Elastic.Xunit.XunitPlumbing;
using Nest;
using Tests.Framework.EndpointTests;
using static Tests.Framework.EndpointTests.UrlTester;

namespace Tests.Cat.CatNodeAttributes
{
	public class CatNodeAttributesUrlTests : UrlTestsBase
	{
		[U] public override async Task Urls() => await GET("/_cat/nodeattrs")
			.Fluent(c => c.Cat.NodeAttributes())
			.Request(c => c.Cat.NodeAttributes(new CatNodeAttributesRequest()))
			.FluentAsync(c => c.Cat.NodeAttributesAsync())
			.RequestAsync(c => c.Cat.NodeAttributesAsync(new CatNodeAttributesRequest()));
	}
}
