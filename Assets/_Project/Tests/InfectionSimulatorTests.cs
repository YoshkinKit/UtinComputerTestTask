using System.Collections.Generic;
using Game.Obstacles;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Тесты чистой симуляции волны заражения препятствий.</summary>
    public class InfectionSimulatorTests
    {
        // Параметры совпадают со значениями по умолчанию InfectionConfig, чтобы тесты
        // отражали реальный баланс игры, а не искусственно подобранные числа.
        private const float RadiusPerShotRadius = 3.5f;
        private const float SpreadEfficiency = 0.85f;
        private const float EnergyCostPerMeter = 1.0f;
        private const float MinEnergy = 0.01f;

        private static InfectionSettings DefaultSettings()
        {
            return new InfectionSettings(RadiusPerShotRadius, SpreadEfficiency, EnergyCostPerMeter, MinEnergy);
        }

        private static InfectionGraph BuildChain(int count, float sphereRadius, float gap, float maxNeighborGap)
        {
            var nodes = new List<ObstacleNode>(count);
            float spacing = 2f * sphereRadius + gap;
            for (int i = 0; i < count; i++)
            {
                nodes.Add(new ObstacleNode(new Vector3(i * spacing, 0f, 0f), sphereRadius));
            }

            return new InfectionGraph(nodes, maxNeighborGap);
        }

        private static HashSet<int> IndicesOf(List<InfectionHit> hits)
        {
            var set = new HashSet<int>();
            foreach (InfectionHit hit in hits)
            {
                set.Add(hit.Index);
            }

            return set;
        }

        [Test]
        public void Simulate_DenseChain_DirectHitOnFirst_DestroysEntireChain()
        {
            // Зазор 0.05 м между 10 сферами радиуса 0.3 м — плотный кластер.
            InfectionGraph graph = BuildChain(count: 10, sphereRadius: 0.3f, gap: 0.05f, maxNeighborGap: 2.5f);
            var results = new List<InfectionHit>();

            InfectionSimulator.Simulate(graph, impactPoint: graph.GetNode(0).Position, shotRadius: 0.4f,
                DefaultSettings(), results);

            Assert.AreEqual(10, results.Count, "dense chain should be destroyed entirely by the wave");
            HashSet<int> indices = IndicesOf(results);
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(indices.Contains(i), $"node {i} expected to be infected");
            }
        }

        [Test]
        public void Simulate_IsolatedSphereFarAway_IsNotInfected()
        {
            var nodes = new List<ObstacleNode>
            {
                new ObstacleNode(new Vector3(10f, 0f, 0f), 0.5f)
            };
            var graph = new InfectionGraph(nodes, maxNeighborGap: 2.5f);
            var results = new List<InfectionHit>();

            InfectionSimulator.Simulate(graph, impactPoint: Vector3.zero, shotRadius: 1.0f, DefaultSettings(), results);

            Assert.AreEqual(0, results.Count, "obstacle 10m away from the impact must not be infected");
        }

        [Test]
        public void Simulate_DirectHitOnIsolatedSphere_DestroysIt()
        {
            var nodes = new List<ObstacleNode>
            {
                new ObstacleNode(new Vector3(5f, 0f, 0f), 0.5f)
            };
            var graph = new InfectionGraph(nodes, maxNeighborGap: 2.5f);
            var results = new List<InfectionHit>();

            InfectionSimulator.Simulate(graph, impactPoint: graph.GetNode(0).Position, shotRadius: 0.3f,
                DefaultSettings(), results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(0, results[0].Index);
            Assert.AreEqual(0, results[0].Depth);
        }

        [Test]
        public void Simulate_LargerShotRadius_ResultsAreSupersetOfSmallerRadius()
        {
            InfectionGraph graph = BuildChain(count: 10, sphereRadius: 0.3f, gap: 0.05f, maxNeighborGap: 2.5f);
            Vector3 impact = graph.GetNode(0).Position;

            var smallResults = new List<InfectionHit>();
            InfectionSimulator.Simulate(graph, impact, shotRadius: 0.15f, DefaultSettings(), smallResults);

            var largeResults = new List<InfectionHit>();
            InfectionSimulator.Simulate(graph, impact, shotRadius: 0.4f, DefaultSettings(), largeResults);

            HashSet<int> smallSet = IndicesOf(smallResults);
            HashSet<int> largeSet = IndicesOf(largeResults);

            Assert.Greater(smallSet.Count, 0, "sanity: small shot should still infect something");
            Assert.Greater(largeSet.Count, smallSet.Count, "larger shot should infect strictly more nodes here");

            foreach (int index in smallSet)
            {
                Assert.IsTrue(largeSet.Contains(index),
                    $"node {index} infected by smaller radius must also be infected by larger radius");
            }
        }

        [Test]
        public void Simulate_SameInputsTwice_ProducesIdenticalResults()
        {
            InfectionGraph graph = BuildChain(count: 10, sphereRadius: 0.3f, gap: 0.05f, maxNeighborGap: 2.5f);
            Vector3 impact = graph.GetNode(0).Position;

            var firstRun = new List<InfectionHit>();
            InfectionSimulator.Simulate(graph, impact, shotRadius: 0.4f, DefaultSettings(), firstRun);

            var secondRun = new List<InfectionHit>();
            InfectionSimulator.Simulate(graph, impact, shotRadius: 0.4f, DefaultSettings(), secondRun);

            Assert.AreEqual(firstRun.Count, secondRun.Count);
            for (int i = 0; i < firstRun.Count; i++)
            {
                Assert.AreEqual(firstRun[i].Index, secondRun[i].Index, $"index mismatch at position {i}");
                Assert.AreEqual(firstRun[i].Depth, secondRun[i].Depth, $"depth mismatch at position {i}");
                Assert.AreEqual(firstRun[i].Energy, secondRun[i].Energy, 1e-9f, $"energy mismatch at position {i}");
            }
        }

        [Test]
        public void Simulate_ResultsAreSortedByDepthThenIndex()
        {
            InfectionGraph graph = BuildChain(count: 10, sphereRadius: 0.3f, gap: 0.05f, maxNeighborGap: 2.5f);
            var results = new List<InfectionHit>();

            InfectionSimulator.Simulate(graph, graph.GetNode(0).Position, shotRadius: 0.4f, DefaultSettings(), results);

            for (int i = 1; i < results.Count; i++)
            {
                InfectionHit previous = results[i - 1];
                InfectionHit current = results[i];
                bool orderedCorrectly = current.Depth > previous.Depth ||
                                         (current.Depth == previous.Depth && current.Index > previous.Index);
                Assert.IsTrue(orderedCorrectly, $"results must be sorted by (Depth, Index) at position {i}");
            }
        }

        [Test]
        public void Simulate_SparseChain_DestroysSignificantlyFewerNodesThanDenseChain()
        {
            InfectionGraph denseChain = BuildChain(count: 10, sphereRadius: 0.3f, gap: 0.05f, maxNeighborGap: 2.5f);
            InfectionGraph sparseChain = BuildChain(count: 10, sphereRadius: 0.3f, gap: 2.0f, maxNeighborGap: 2.5f);

            var denseResults = new List<InfectionHit>();
            InfectionSimulator.Simulate(denseChain, denseChain.GetNode(0).Position, shotRadius: 0.4f,
                DefaultSettings(), denseResults);

            var sparseResults = new List<InfectionHit>();
            InfectionSimulator.Simulate(sparseChain, sparseChain.GetNode(0).Position, shotRadius: 0.4f,
                DefaultSettings(), sparseResults);

            Assert.Greater(denseResults.Count, sparseResults.Count,
                "the same shot should infect far fewer nodes when obstacles are spread apart");
            Assert.AreEqual(10, denseResults.Count);
            Assert.AreEqual(1, sparseResults.Count, "sparse chain: only the directly hit node should die");
        }

        [Test]
        public void Simulate_DeadNodes_AreNeverInfected()
        {
            InfectionGraph graph = BuildChain(count: 3, sphereRadius: 0.3f, gap: 0.05f, maxNeighborGap: 2.5f);
            graph.Kill(1);
            var results = new List<InfectionHit>();

            InfectionSimulator.Simulate(graph, graph.GetNode(0).Position, shotRadius: 0.4f, DefaultSettings(), results);

            foreach (InfectionHit hit in results)
            {
                Assert.AreNotEqual(1, hit.Index, "dead node must never appear in results");
            }
        }
    }
}
