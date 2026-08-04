using System.Collections.Generic;
using Game.Level;
using Game.Obstacles;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Тесты бинарного поиска минимального радиуса выстрела, расчищающего сегмент.</summary>
    public class LevelSolverTests
    {
        private const float RadiusPerShotRadius = 3.5f;
        private const float SpreadEfficiency = 0.85f;
        private const float EnergyCostPerMeter = 1.0f;
        private const float MinEnergy = 0.01f;

        private static InfectionSettings DefaultSettings()
        {
            return new InfectionSettings(RadiusPerShotRadius, SpreadEfficiency, EnergyCostPerMeter, MinEnergy);
        }

        // Единственное препятствие точно на луче выстрела (ось X): origin -> прямое попадание.
        private static InfectionGraph BuildSingleObstacleOnRay()
        {
            var nodes = new List<ObstacleNode> { new ObstacleNode(new Vector3(0f, 0f, 0f), 0.3f) };
            return new InfectionGraph(nodes, maxNeighborGap: 2.5f);
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

        [Test]
        public void TryFindMinimalShotRadius_SolvableSegment_FindsRadiusThatActuallyClearsIt()
        {
            InfectionGraph graph = BuildSingleObstacleOnRay();
            Vector3 origin = new Vector3(-1f, 0f, 0f);
            Vector3 direction = Vector3.right;
            Vector3 segmentA = new Vector3(-1f, 0f, 0f);
            Vector3 segmentB = new Vector3(1f, 0f, 0f);
            const float playerRadius = 0.3f;

            bool found = LevelSolver.TryFindMinimalShotRadius(graph, origin, direction, segmentA, segmentB,
                playerRadius, DefaultSettings(), maxShotRadius: 5f, tolerance: 0.001f, out float minRadius);

            Assert.IsTrue(found, "a solution must exist within the generous max radius");
            Assert.Greater(minRadius, 0f);

            // Прогоняем найденный радиус независимо на свежем клоне и проверяем: сегмент реально расчищен.
            InfectionGraph resultClone = LevelSolver.SimulateShotOnClone(graph, origin, direction, minRadius, DefaultSettings());
            bool clear = PathGeometry.IsSegmentClear(resultClone, segmentA, segmentB, playerRadius);

            Assert.IsTrue(clear, "the radius returned by the binary search must actually clear the segment");
        }

        [Test]
        public void TryFindMinimalShotRadius_ObstacleUnkillableWithinBudget_ReturnsFalse()
        {
            InfectionGraph graph = BuildSingleObstacleOnRay();
            Vector3 origin = new Vector3(-1f, 0f, 0f);
            Vector3 direction = Vector3.right;
            Vector3 segmentA = new Vector3(-1f, 0f, 0f);
            Vector3 segmentB = new Vector3(1f, 0f, 0f);
            const float playerRadius = 0.3f;

            // Крошечный бюджет радиуса: даже прямое попадание даёт энергию ниже MinEnergy,
            // узел никогда не заражается -> сегмент остаётся заблокирован при любом radius <= maxShotRadius.
            bool found = LevelSolver.TryFindMinimalShotRadius(graph, origin, direction, segmentA, segmentB,
                playerRadius, DefaultSettings(), maxShotRadius: 0.001f, tolerance: 0.0001f, out float minRadius);

            Assert.IsFalse(found, "insufficient mass budget must be honestly reported as 'no solution'");
        }

        [Test]
        public void TryFindMinimalShotRadius_FoundRadius_IsTightWithinTolerance()
        {
            InfectionGraph graph = BuildSingleObstacleOnRay();
            Vector3 origin = new Vector3(-1f, 0f, 0f);
            Vector3 direction = Vector3.right;
            Vector3 segmentA = new Vector3(-1f, 0f, 0f);
            Vector3 segmentB = new Vector3(1f, 0f, 0f);
            const float playerRadius = 0.3f;
            const float tolerance = 0.001f;

            bool found = LevelSolver.TryFindMinimalShotRadius(graph, origin, direction, segmentA, segmentB,
                playerRadius, DefaultSettings(), maxShotRadius: 5f, tolerance: tolerance, out float minRadius);

            Assert.IsTrue(found);

            float slightlySmaller = Mathf.Max(0f, minRadius - 2f * tolerance);
            InfectionGraph smallerClone = LevelSolver.SimulateShotOnClone(graph, origin, direction, slightlySmaller, DefaultSettings());
            bool clearWithSmallerRadius = PathGeometry.IsSegmentClear(smallerClone, segmentA, segmentB, playerRadius);

            Assert.IsFalse(clearWithSmallerRadius,
                $"a radius {2 * tolerance} below the found minimum ({minRadius}) must NOT clear the segment, " +
                "otherwise the search did not converge to a tight minimum");
        }

        [Test]
        public void TryFindMinimalShotRadius_DenseCluster_NeedsSmallerRadiusThanSparseClusterOfSameSize()
        {
            const int count = 5;
            const float sphereRadius = 0.3f;
            const float maxNeighborGap = 2.5f;

            InfectionGraph denseChain = BuildChain(count, sphereRadius, gap: 0.05f, maxNeighborGap);
            InfectionGraph sparseChain = BuildChain(count, sphereRadius, gap: 2.0f, maxNeighborGap);

            // Отрезок совпадает с осью цепочки: чтобы стать чистым, должны погибнуть ВСЕ узлы.
            Vector3 origin = new Vector3(-1f, 0f, 0f);
            Vector3 direction = Vector3.right;
            const float playerRadius = 0.3f;
            const float maxShotRadius = 10f;
            const float tolerance = 0.01f;

            float denseSpan = (count - 1) * (2f * sphereRadius + 0.05f);
            float sparseSpan = (count - 1) * (2f * sphereRadius + 2.0f);
            Vector3 denseSegmentB = new Vector3(denseSpan + 1f, 0f, 0f);
            Vector3 sparseSegmentB = new Vector3(sparseSpan + 1f, 0f, 0f);

            bool denseFound = LevelSolver.TryFindMinimalShotRadius(denseChain, origin, direction, origin, denseSegmentB,
                playerRadius, DefaultSettings(), maxShotRadius, tolerance, out float denseMinRadius);
            bool sparseFound = LevelSolver.TryFindMinimalShotRadius(sparseChain, origin, direction, origin, sparseSegmentB,
                playerRadius, DefaultSettings(), maxShotRadius, tolerance, out float sparseMinRadius);

            Assert.IsTrue(denseFound, "dense cluster must be fully clearable within the generous budget");
            Assert.IsTrue(sparseFound, "sparse cluster must still be clearable (direct seeding reaches every node eventually)");
            Assert.Less(denseMinRadius, sparseMinRadius,
                "a dense cluster should require a strictly smaller shot radius than an equally-sized sparse one, " +
                "because the infection wave propagates between close neighbours almost for free");
        }
    }
}
