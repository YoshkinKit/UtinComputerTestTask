using Game.Config;
using Game.Level;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Тесты бюджета массы на том балансе, который уходит в билд. Здесь проверяется не
    /// «метод работает», а само требование ТЗ: стартового размера шара хватает на прохождение
    /// с запасом 20%. Заодно тесты держат баланс с обеих сторон — уровень не должен ни стать
    /// непроходимым, ни выродиться в бесплатный.
    /// </summary>
    public class LevelBudgetTests
    {
        /// <summary>
        /// Верхняя граница запаса. Формально ТЗ требует только «не меньше 20%», но запас в
        /// разы означает, что шар доходит до двери почти не похудев — а по ТЗ он обязан
        /// заметно уменьшиться. Так что это тоже требование, просто с другой стороны.
        /// </summary>
        private const float MaxReasonableReserve = 1.0f;

        private LevelConfig _levelConfig;
        private PlayerConfig _playerConfig;
        private InfectionConfig _infectionConfig;
        private LevelBudget _budget;

        [SetUp]
        public void SetUp()
        {
            _levelConfig = ScriptableObject.CreateInstance<LevelConfig>();
            _playerConfig = ScriptableObject.CreateInstance<PlayerConfig>();
            _infectionConfig = ScriptableObject.CreateInstance<InfectionConfig>();
            _budget = LevelBudgetValidator.Evaluate(_levelConfig, _playerConfig, _infectionConfig);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_levelConfig);
            Object.DestroyImmediate(_playerConfig);
            Object.DestroyImmediate(_infectionConfig);
        }

        [Test]
        public void ShippedBalance_LeavesAtLeastTheRequiredTwentyPercentReserve()
        {
            Assert.IsTrue(_budget.Completable, $"the shipped level must be completable: {_budget.Failure}");
            Assert.GreaterOrEqual(_budget.Reserve, LevelBudgetValidator.RequiredReserve,
                $"the spec requires the starting ball to suffice with a 20% margin; " +
                $"needed {_budget.RequiredMass:F2} of {_budget.AvailableMass:F2} available");
        }

        [Test]
        public void ShippedBalance_IsNotDegenerate()
        {
            Assert.LessOrEqual(_budget.Reserve, MaxReasonableReserve,
                $"a reserve of {_budget.Reserve * 100f:F0}% means the ball reaches the door barely having " +
                "shrunk — the whole point of the game is that shots are paid for with the ball's own size");
        }

        [Test]
        public void ShippedBalance_ShrinksTheBallTowardsTheDesignedCorridorWidth()
        {
            // Коридор у двери рассчитан на ExpectedEndRadius. Шар не обязан попасть в него
            // точно, но обязан прийти к двери примерно того размера — иначе профиль сужения
            // коридора и баланс живут отдельными жизнями.
            float designed = _levelConfig.ExpectedEndRadius;

            Assert.Less(_budget.FinalRadius, _playerConfig.StartRadius,
                "the ball must be visibly smaller at the door than at the start");
            Assert.LessOrEqual(_budget.FinalRadius, designed * 1.3f,
                $"the ball arrives at the door with radius {_budget.FinalRadius:F2} m while the corridor " +
                $"there is built for {designed:F2} m — balance and level geometry have drifted apart");
        }

        [Test]
        public void ShippedBalance_RequiresSeveralShots()
        {
            Assert.GreaterOrEqual(_budget.ShotCount, 5,
                "a level cleared in a couple of shots does not demonstrate the mechanic");
        }
    }
}
