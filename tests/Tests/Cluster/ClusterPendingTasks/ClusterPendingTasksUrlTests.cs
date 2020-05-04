// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

﻿using System.Threading.Tasks;
using Elastic.Xunit.XunitPlumbing;
using Nest;
using Tests.Framework.EndpointTests;
using static Tests.Framework.EndpointTests.UrlTester;

namespace Tests.Cluster.ClusterPendingTasks
{
	public class ClusterPendingTasksUrlTests : UrlTestsBase
	{
		[U] public override async Task Urls() => await GET("/_cluster/pending_tasks")
			.Fluent(c => c.Cluster.PendingTasks())
			.Request(c => c.Cluster.PendingTasks(new ClusterPendingTasksRequest()))
			.FluentAsync(c => c.Cluster.PendingTasksAsync())
			.RequestAsync(c => c.Cluster.PendingTasksAsync(new ClusterPendingTasksRequest()));
	}
}
