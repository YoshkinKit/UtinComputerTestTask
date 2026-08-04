using System.Collections.Generic;
using Game.Config;
using Game.Level;
using Game.Obstacles;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Тесты генератора уровня. Проверяется не «нарисовалось что-то похожее», а ровно те
    /// гарантии, на которые опирается геймплей: коридор проходим, каждый завал перекрывает
    /// свой сегмент и не задевает предыдущий, одиночные препятствия действительно изолированы
    /// в графе заражения, а уровень в принципе проходим.
    /// </summary>
    public class LevelBuilderTests
    {
        /// <summary>Тот же крошечный клиренс, которым генератор проверяет «завал перекрывает сегмент».</summary>
        private const float TinyBallRadius = 0.15f;

        private LevelConfig _levelConfig;
        private PlayerConfig _playerConfig;
        private InfectionConfig _infectionConfig;
        private LevelLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _levelConfig = ScriptableObject.CreateInstance<LevelConfig>();
            _playerConfig = ScriptableObject.CreateInstance<PlayerConfig>();
            _infectionConfig = ScriptableObject.CreateInstance<InfectionConfig>();
            _layout = LevelBuilder.Build(_levelConfig, _infectionConfig.MaxNeighborGap);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_levelConfig);
            Object.DestroyImmediate(_playerConfig);
            Object.DestroyImmediate(_infectionConfig);
        }

        [Test]
        public void CorridorWalls_NeverIntrudeIntoTheCorridor()
        {
            InfectionGraph walls = BuildGraph(ObstacleRole.CorridorWall);
            Vector3[] points = _layout.PathPoints;

            for (int i = 0; i < points.Length - 1; i++)
            {
                // Ровно тот клиренс, которым проходимость сегмента проверяется в рантайме:
                // радиус игрока, под который построен коридор, умноженный на запас из PlayerConfig.
                float playerRadius = _layout.CorridorWidths[i] / (2f * _levelConfig.CorridorMarginFactor);
                float radius = playerRadius * _playerConfig.ClearanceFactor;

                Assert.IsTrue(PathGeometry.IsSegmentClear(walls, points[i], points[i + 1], radius),
                    $"corridor walls must never block segment {i} -> {i + 1}: the corridor is what the player " +
                    "is supposed to hop through after clearing the blockers");
            }
        }

        [Test]
        public void EveryBlocker_ClosesItsOwnSegment_AndLeavesThePassedOneOpen()
        {
            Vector3[] points = _layout.PathPoints;
            Assert.IsNotEmpty(_layout.Blockers, "the level must contain blockers, otherwise there is no gameplay");

            foreach (BlockerSpec blocker in _layout.Blockers)
            {
                int stop = blocker.StopIndex;
                InfectionGraph group = BuildGraph(blocker);
                float maxBallRadius = 0.5f * _layout.CorridorWidths[stop];

                Assert.IsFalse(PathGeometry.IsSegmentClear(group, points[stop], points[stop + 1], TinyBallRadius),
                    $"blocker at stop {stop} must close segment {stop} -> {stop + 1} even for a nearly-critical " +
                    "player ball, otherwise the player would simply hop past it");

                Assert.IsTrue(PathGeometry.IsSegmentClear(group, points[stop - 1], points[stop], maxBallRadius),
                    $"blocker at stop {stop} must not reach back into segment {stop - 1} -> {stop}: the player " +
                    "has to be able to actually arrive at the stop point before shooting");
            }
        }

        [Test]
        public void EveryBlocker_SitsOnTheRayFromItsStopPointToTheDoor()
        {
            Vector3[] points = _layout.PathPoints;

            foreach (BlockerSpec blocker in _layout.Blockers)
            {
                Vector3 origin = points[blocker.StopIndex];
                Vector3 direction = (_layout.DoorPosition - origin).normalized;

                InfectionGraph group = BuildGraph(blocker);
                bool hit = PathGeometry.RaycastFirstNode(group, origin, direction,
                    shotRadius: 0.1f, out _, out _);

                // Выстрел в игре летит строго на дверь, поэтому «завал стоит на пути» и
                // «выстрел в дверь попадает в завал» — это два разных утверждения, и нужно второе.
                Assert.IsTrue(hit,
                    $"a shot fired at the door from stop point {blocker.StopIndex} must hit that blocker; " +
                    "otherwise the player cannot clear the very obstacle that stopped them");
            }
        }

        [Test]
        public void SingleBlockers_HaveNoNeighboursInTheInfectionGraph()
        {
            InfectionGraph graph = LevelWalkthrough.BuildGraph(_layout, _infectionConfig.MaxNeighborGap);
            int checkedCount = 0;

            foreach (BlockerSpec blocker in _layout.Blockers)
            {
                if (blocker.Role != ObstacleRole.SingleBlocker)
                {
                    continue;
                }

                graph.GetNeighbors(blocker.FirstObstacle, out _, out int neighbourCount);
                Assert.AreEqual(0, neighbourCount,
                    "a single obstacle must be fully isolated in the infection graph — that is exactly what " +
                    "forces the player to spend a small precise shot on it instead of a chain reaction");

                checkedCount++;
            }

            Assert.Greater(checkedCount, 0, "the level must contain at least one isolated single obstacle");
        }

        [Test]
        public void Corridor_NarrowsTowardsTheDoor()
        {
            for (int i = 1; i <= _layout.DoorPointIndex; i++)
            {
                Assert.LessOrEqual(_layout.CorridorWidths[i], _layout.CorridorWidths[i - 1] + 1e-4f,
                    $"corridor width must not grow along the path (point {i})");
            }

            Assert.Less(_layout.CorridorWidths[_layout.DoorPointIndex], _layout.CorridorWidths[0],
                "the corridor at the door must be narrower than at the start — it shrinks together with the ball");
        }

        [Test]
        public void PathDeviation_StaysWithinTheConfiguredLimit()
        {
            Vector3[] points = _layout.PathPoints;

            for (int i = 0; i < _layout.DoorPointIndex; i++)
            {
                Vector3 segment = points[i + 1] - points[i];
                Vector3 toDoor = _layout.DoorPosition - points[i];
                if (segment.sqrMagnitude < 1e-8f || toDoor.sqrMagnitude < 1e-8f)
                {
                    continue;
                }

                Assert.LessOrEqual(Vector3.Angle(segment, toDoor), _levelConfig.MaxPathDeviationDegrees + 0.5f,
                    $"segment {i} deviates from the door direction more than the config allows; the auto-reduced " +
                    "curve amplitude is supposed to guarantee this");
            }
        }

        [Test]
        public void Level_IsCompletableByAnIdealPlayer()
        {
            WalkthroughResult result = LevelWalkthrough.Simulate(_layout, _playerConfig, _infectionConfig);

            Assert.IsTrue(result.Completed, $"the generated level must be solvable: {result.Failure}");
            Assert.Greater(result.ShotCount, 0, "a level without a single required shot is not a level");
            Assert.Greater(result.FinalRadius, _playerConfig.CriticalRadius,
                "the ball must stay above the critical radius all the way to the door");
        }

        [Test]
        public void Build_IsDeterministicForTheSameSeed()
        {
            LevelLayout other = LevelBuilder.Build(_levelConfig, _infectionConfig.MaxNeighborGap);

            Assert.AreEqual(_layout.Obstacles.Length, other.Obstacles.Length);
            for (int i = 0; i < _layout.Obstacles.Length; i++)
            {
                Assert.AreEqual(_layout.Obstacles[i].Position, other.Obstacles[i].Position);
                Assert.AreEqual(_layout.Obstacles[i].Radius, other.Obstacles[i].Radius);
            }
        }

        private InfectionGraph BuildGraph(ObstacleRole role)
        {
            var nodes = new List<ObstacleNode>();
            foreach (ObstacleSpec spec in _layout.Obstacles)
            {
                if (spec.Role == role)
                {
                    nodes.Add(new ObstacleNode(spec.Position, spec.Radius));
                }
            }

            return new InfectionGraph(nodes, _infectionConfig.MaxNeighborGap);
        }

        private InfectionGraph BuildGraph(BlockerSpec blocker)
        {
            var nodes = new List<ObstacleNode>(blocker.ObstacleCount);
            for (int i = 0; i < blocker.ObstacleCount; i++)
            {
                ObstacleSpec spec = _layout.Obstacles[blocker.FirstObstacle + i];
                nodes.Add(new ObstacleNode(spec.Position, spec.Radius));
            }

            return new InfectionGraph(nodes, _infectionConfig.MaxNeighborGap);
        }
    }
}
