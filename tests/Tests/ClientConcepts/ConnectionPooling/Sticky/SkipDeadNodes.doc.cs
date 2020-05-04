// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Elastic.Xunit.XunitPlumbing;
using Elasticsearch.Net;
using Elasticsearch.Net.VirtualizedCluster;
using Elasticsearch.Net.VirtualizedCluster.Audit;
using Elasticsearch.Net.VirtualizedCluster.Providers;
using FluentAssertions;
using Tests.Framework;
using static Elasticsearch.Net.VirtualizedCluster.Rules.TimesHelper;
using static Elasticsearch.Net.AuditEvent;

namespace Tests.ClientConcepts.ConnectionPooling.Sticky
{
	public class SkipDeadNodes
	{
		/** Sticky - Skipping Dead Nodes
		 * When selecting nodes the connection pool will try and skip all the nodes that are marked dead.
		*/
		protected int NumberOfNodes = 3;

		[U] public void EachViewDoesNotSkip()
		{
			var seeds = Enumerable.Range(9200, NumberOfNodes).Select(p => new Node(new Uri("http://localhost:" + p))).ToList();
			var pool = new StickyConnectionPool(seeds);
			for (var i = 0; i < 20; i++)
			{
				var node = pool.CreateView().First();
				node.Uri.Port.Should().Be(9200);
				node = pool.CreateView().First();
				node.Uri.Port.Should().Be(9200);
				node = pool.CreateView().First();
				node.Uri.Port.Should().Be(9200);
			}
		}

		[U] public void EachViewSeesNextButSkipsTheDeadNode()
		{
			var seeds = Enumerable.Range(9200, NumberOfNodes).Select(p => new Node(new Uri("http://localhost:" + p))).ToList();
			seeds.First().MarkDead(DateTime.Now.AddDays(1));
			var pool = new StickyConnectionPool(seeds);
			for (var i = 0; i < 20; i++)
			{
				var node = pool.CreateView().First();
				node.Uri.Port.Should().Be(9201);
				node = pool.CreateView().First();
				node.Uri.Port.Should().Be(9201);
			}
			/** After we mark the first node alive again we expect it to be hit again*/
			seeds.First().MarkAlive();
			for (var i = 0; i < 20; i++)
			{
				var node = pool.CreateView().First();
				node.Uri.Port.Should().Be(9200);
				node = pool.CreateView().First();
				node.Uri.Port.Should().Be(9200);
				node = pool.CreateView().First();
				node.Uri.Port.Should().Be(9200);
			}
		}

		[U] public void ViewSeesResurrectedNodes()
		{
			var dateTimeProvider = new TestableDateTimeProvider();
			var seeds = Enumerable.Range(9200, NumberOfNodes).Select(p => new Node(new Uri("http://localhost:" + p))).ToList();
			seeds.First().MarkDead(dateTimeProvider.Now().AddDays(1));
			var pool = new StickyConnectionPool(seeds, dateTimeProvider: dateTimeProvider);
			for (var i = 0; i < 20; i++)
			{
				var node = pool.CreateView().First();
				node.Uri.Port.Should().Be(9201);
				node = pool.CreateView().First();
				node.Uri.Port.Should().Be(9201);
			}
			/** If we forward our clock 2 days the node that was marked dead until tomorrow (or yesterday!) should be resurrected */
			dateTimeProvider.ChangeTime(d => d.AddDays(2));
			var n = pool.CreateView().First();
			n.Uri.Port.Should().Be(9200);
			n = pool.CreateView().First();
			n.Uri.Port.Should().Be(9200);
			n = pool.CreateView().First();
			n.Uri.Port.Should().Be(9200);
			n.IsResurrected.Should().BeTrue();
		}

		[U, SuppressMessage("AsyncUsage", "AsyncFixer001:Unnecessary async/await usage", Justification = "Its a test")]
		public async Task FallsOverDeadNodes()
		{
			/** A cluster with 2 nodes where the second node fails on ping */
			var audit = new Auditor(() => VirtualClusterWith
				.Nodes(4)
				.ClientCalls(p => p.Succeeds(Always))
				.ClientCalls(p => p.OnPort(9200).FailAlways())
				.ClientCalls(p => p.OnPort(9201).FailAlways())
				.StickyConnectionPool()
				.Settings(p => p.DisablePing())
			);

			await audit.TraceCalls(
				/** The first call goes to 9200 which succeeds */
				new ClientCall {
					{ BadResponse, 9200},
					{ BadResponse, 9201},
					{ HealthyResponse, 9202},
					{ pool => pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(2) }
				},
				/** The 2nd call does a ping on 9201 because its used for the first time.
				* It fails so we wrap over to node 9202 */
				new ClientCall {
					{ HealthyResponse, 9202},
					/** Finally we assert that the connectionpool has one node that is marked as dead */
					{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(2) }
				},
				new ClientCall {
					{ HealthyResponse, 9202},
					{ pool => pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(2) }
				}
			);
		}

		[U, SuppressMessage("AsyncUsage", "AsyncFixer001:Unnecessary async/await usage", Justification = "Its a test")]
		public async Task PicksADifferentNodeEachTimeAnodeIsDown()
		{
			/** A cluster with 2 nodes where the second node fails on ping */
			var audit = new Auditor(() => VirtualClusterWith
				.Nodes(4)
				.ClientCalls(p => p.Fails(Always))
				.StickyConnectionPool()
				.Settings(p => p.DisablePing())
			);

			await audit.TraceCalls(
				/** All the calls fail */
				new ClientCall {
					{ BadResponse, 9200},
					{ BadResponse, 9201},
					{ BadResponse, 9202},
					{ BadResponse, 9203},
					{ MaxRetriesReached },
					{ FailedOverAllNodes },
					{ pool => pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
				},
				/** After all our registered nodes are marked dead we want to sample a single dead node
				* each time to quickly see if the cluster is back up. We do not want to retry all 4
				* nodes
				*/
				new ClientCall {
					{ AllNodesDead },
					{ Resurrection, 9200},
					{ BadResponse, 9200},
					{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
				},
				new ClientCall {
					{ AllNodesDead },
					{ Resurrection, 9201},
					{ BadResponse, 9201},
					{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
				},
				new ClientCall {
					{ AllNodesDead },
					{ Resurrection, 9202},
					{ BadResponse, 9202},
					{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
				},
				new ClientCall {
					{ AllNodesDead },
					{ Resurrection, 9203},
					{ BadResponse, 9203},
					{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
				}
			);
		}
	}
}
