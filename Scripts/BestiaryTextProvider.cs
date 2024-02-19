using DaggerfallWorkshop.Utility;

namespace DaggerfallBestiaryProject
{
    internal class BestiaryTextProvider : FallbackTextProvider
    {
        public BestiaryTextProvider(ITextProvider fallback)
            : base(fallback)
        {

        }

        public override string GetCustomEnemyName(int enemyId)
        {
            if(BestiaryMod.Instance != null)
            {
                var customEnemies = BestiaryMod.Instance.CustomEnemies;
                if(customEnemies.TryGetValue(enemyId, out var enemy))
                {
                    if (!string.IsNullOrEmpty(enemy.name))
                    {
                        return enemy.name;
                    }
                }
            }

            return base.GetCustomEnemyName(enemyId);
        }
    }
}
