using System.Collections.Generic;
using Game.Level;
using Game.Obstacles;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Тесты чистой геометрии прохода коридора и рейкаста выстрела.</summary>
    public class PathGeometryTests
    {
        private static InfectionGraph BuildGraph(params ObstacleNode[] nodes)
        {
            return new InfectionGraph(nodes, maxNeighborGap: 2.5f);
        }

        [Test]
        public void DistancePointToSegment_PerpendicularPoint_ReturnsCorrectDistance()
        {
            Vector3 a = new Vector3(0f, 0f, 0f);
            Vector3 b = new Vector3(10f, 0f, 0f);
            Vector3 point = new Vector3(5f, 0f, 3f);

            float distance = PathGeometry.DistancePointToSegment(point, a, b);

            Assert.AreEqual(3f, distance, 1e-5f);
        }

        [Test]
        public void DistancePointToSegment_PointBeyondEndpoint_ClampsToEndpoint()
        {
            Vector3 a = new Vector3(0f, 0f, 0f);
            Vector3 b = new Vector3(10f, 0f, 0f);
            Vector3 point = new Vector3(15f, 0f, 0f);

            float distance = PathGeometry.DistancePointToSegment(point, a, b);

            Assert.AreEqual(5f, distance, 1e-5f);
        }

        [Test]
        public void IsSegmentClear_NoObstaclesInTheWay_ReturnsTrue()
        {
            var graph = BuildGraph(
                new ObstacleNode(new Vector3(0f, 0f, 10f), 0.5f));

            bool clear = PathGeometry.IsSegmentClear(graph, Vector3.zero, new Vector3(10f, 0f, 0f), radius: 0.5f);

            Assert.IsTrue(clear);
        }

        [Test]
        public void IsSegmentClear_ObstacleBlockingPath_ReturnsFalse()
        {
            var graph = BuildGraph(
                new ObstacleNode(new Vector3(5f, 0f, 0f), 0.5f));

            bool clear = PathGeometry.IsSegmentClear(graph, Vector3.zero, new Vector3(10f, 0f, 0f), radius: 0.5f);

            Assert.IsFalse(clear);
        }

        [Test]
        public void IsSegmentClear_DeadObstacle_IsIgnored()
        {
            var graph = BuildGraph(
                new ObstacleNode(new Vector3(5f, 0f, 0f), 0.5f));
            graph.Kill(0);

            bool clear = PathGeometry.IsSegmentClear(graph, Vector3.zero, new Vector3(10f, 0f, 0f), radius: 0.5f);

            Assert.IsTrue(clear);
        }

        [Test]
        public void FindFirstBlocker_NoBlockers_ReturnsMinusOne()
        {
            var graph = BuildGraph(
                new ObstacleNode(new Vector3(0f, 0f, 20f), 0.5f));

            int blocker = PathGeometry.FindFirstBlocker(graph, Vector3.zero, new Vector3(10f, 0f, 0f), radius: 0.5f);

            Assert.AreEqual(-1, blocker);
        }

        [Test]
        public void FindFirstBlocker_MultipleBlockersOnAxis_ReturnsEarliestAlongSegment()
        {
            // Индекс 0 дальше вдоль отрезка (t=0.8), индекс 1 ближе (t=0.2). Оба блокируют отрезок.
            var graph = BuildGraph(
                new ObstacleNode(new Vector3(8f, 0f, 0f), 0.5f),
                new ObstacleNode(new Vector3(2f, 0f, 0f), 0.5f));

            int blocker = PathGeometry.FindFirstBlocker(graph, Vector3.zero, new Vector3(10f, 0f, 0f), radius: 0.5f);

            Assert.AreEqual(1, blocker);
        }

        [Test]
        public void FindFirstBlocker_PrefersEarlierAlongSegment_OverEuclideanNearestToA()
        {
            // Узел 0 ("F") лежит прямо на отрезке дальше по пути (t=0.3), сырое евклидово
            // расстояние до a = 3.0. Узел 1 ("N") лежит гораздо ближе к началу пути (t=0.05),
            // но сильно смещён в сторону (z=4), поэтому его сырое евклидово расстояние до a
            // больше (~4.03). Старый критерий "минимум расстояния до a" выбрал бы узел 0,
            // новый критерий "минимальный t вдоль отрезка" обязан выбрать узел 1 — именно он
            // встречается первым, если двигаться от a к b.
            var graph = BuildGraph(
                new ObstacleNode(new Vector3(3f, 0f, 0f), 0.5f),
                new ObstacleNode(new Vector3(0.5f, 0f, 4f), 4.3f));

            Vector3 a = Vector3.zero;
            Vector3 b = new Vector3(10f, 0f, 0f);
            const float queryRadius = 0.3f;

            // Оба узла действительно блокируют отрезок при выбранных радиусах.
            Assert.Less(PathGeometry.DistancePointToSegment(graph.GetNode(0).Position, a, b),
                graph.GetNode(0).Radius + queryRadius, "node 0 (F) must block the segment");
            Assert.Less(PathGeometry.DistancePointToSegment(graph.GetNode(1).Position, a, b),
                graph.GetNode(1).Radius + queryRadius, "node 1 (N) must block the segment");

            int blocker = PathGeometry.FindFirstBlocker(graph, a, b, queryRadius);

            Assert.AreEqual(1, blocker, "should return the node encountered earliest along the segment (smallest t), not the raw-nearest-to-a node");
        }

        [Test]
        public void RaycastFirstNode_HitsNearestSphereAlongRay()
        {
            var graph = BuildGraph(
                new ObstacleNode(new Vector3(10f, 0f, 0f), 1f),
                new ObstacleNode(new Vector3(4f, 0f, 0f), 1f));

            bool hit = PathGeometry.RaycastFirstNode(graph, Vector3.zero, Vector3.right,
                shotRadius: 0.5f, out int index, out Vector3 impactPoint);

            Assert.IsTrue(hit);
            Assert.AreEqual(1, index);
            // Раздутая сфера (радиус 1 + shotRadius 0.5 = 1.5) с центром в x=4 -> касание в x=2.5.
            Vector3 expectedImpact = new Vector3(2.5f, 0f, 0f);
            Assert.Less(Vector3.Distance(expectedImpact, impactPoint), 1e-4f,
                $"expected impact point near {expectedImpact}, got {impactPoint}");
        }

        [Test]
        public void RaycastFirstNode_NoObstaclesOnRay_ReturnsFalse()
        {
            var graph = BuildGraph(
                new ObstacleNode(new Vector3(0f, 0f, 10f), 1f));

            bool hit = PathGeometry.RaycastFirstNode(graph, Vector3.zero, Vector3.right,
                shotRadius: 0.5f, out int index, out Vector3 impactPoint);

            Assert.IsFalse(hit);
            Assert.AreEqual(-1, index);
        }

        [Test]
        public void RaycastFirstNode_DeadObstacle_IsIgnored()
        {
            var graph = BuildGraph(
                new ObstacleNode(new Vector3(4f, 0f, 0f), 1f));
            graph.Kill(0);

            bool hit = PathGeometry.RaycastFirstNode(graph, Vector3.zero, Vector3.right,
                shotRadius: 0.5f, out int index, out Vector3 impactPoint);

            Assert.IsFalse(hit);
        }
    }
}
