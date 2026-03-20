namespace ArenaShooter;

public static class WeaponStats
{
    public struct Stats
    {
        public float HitStopNormal;
        public float HitStopFinisher;
        public float HitStopKill;
        public float ShakeIntensity;
        public float ShakeDuration;
        public float KnockbackForce;
        public float KnockbackUp;
        public int Damage;
        public int FinisherDamage;
        public float AttackSpeed;
        public float ComboWindow;
        public float ComboCooldown;
        public bool IsRanged;
        public string DisplayName;
        public float Weight; // 0-1, used for particle scaling
        public float ActiveTime; // how long hitbox stays out
    }

    public static Stats Get(WeaponType type) => type switch
    {
        WeaponType.None => new Stats { HitStopNormal = 0.06f, HitStopFinisher = 0.12f, HitStopKill = 0.15f, ShakeIntensity = 6f, ShakeDuration = 0.12f, KnockbackForce = 80f, KnockbackUp = -40f, Damage = 1, FinisherDamage = 2, AttackSpeed = 0.06f, ComboWindow = 0.35f, ComboCooldown = 0.25f, DisplayName = "Fists", Weight = 0f, ActiveTime = 0.06f },
        WeaponType.Stick => new Stats { HitStopNormal = 0.09f, HitStopFinisher = 0.15f, HitStopKill = 0.21f, ShakeIntensity = 8f, ShakeDuration = 0.16f, KnockbackForce = 120f, KnockbackUp = -50f, Damage = 1, FinisherDamage = 2, AttackSpeed = 0.1f, ComboWindow = 0.4f, ComboCooldown = 0.35f, DisplayName = "Stick", Weight = 0.15f, ActiveTime = 0.1f },
        WeaponType.Dagger => new Stats { HitStopNormal = 0.045f, HitStopFinisher = 0.09f, HitStopKill = 0.12f, ShakeIntensity = 4f, ShakeDuration = 0.1f, KnockbackForce = 40f, KnockbackUp = -20f, Damage = 1, FinisherDamage = 2, AttackSpeed = 0.04f, ComboWindow = 0.25f, ComboCooldown = 0.15f, DisplayName = "Dagger", Weight = 0.1f, ActiveTime = 0.06f },
        WeaponType.Whip => new Stats { HitStopNormal = 0.075f, HitStopFinisher = 0.12f, HitStopKill = 0.18f, ShakeIntensity = 6f, ShakeDuration = 0.14f, KnockbackForce = 60f, KnockbackUp = -30f, Damage = 1, FinisherDamage = 2, AttackSpeed = 0.12f, ComboWindow = 0.45f, ComboCooldown = 0.3f, DisplayName = "Whip", Weight = 0.2f, ActiveTime = 0.15f },
        WeaponType.Sword => new Stats { HitStopNormal = 0.12f, HitStopFinisher = 0.18f, HitStopKill = 0.24f, ShakeIntensity = 10f, ShakeDuration = 0.2f, KnockbackForce = 150f, KnockbackUp = -60f, Damage = 2, FinisherDamage = 3, AttackSpeed = 0.1f, ComboWindow = 0.4f, ComboCooldown = 0.3f, DisplayName = "Sword", Weight = 0.4f, ActiveTime = 0.12f },
        WeaponType.Axe => new Stats { HitStopNormal = 0.15f, HitStopFinisher = 0.21f, HitStopKill = 0.3f, ShakeIntensity = 12f, ShakeDuration = 0.24f, KnockbackForce = 180f, KnockbackUp = -80f, Damage = 2, FinisherDamage = 4, AttackSpeed = 0.14f, ComboWindow = 0.5f, ComboCooldown = 0.4f, DisplayName = "Axe", Weight = 0.6f, ActiveTime = 0.14f },
        WeaponType.Club => new Stats { HitStopNormal = 0.15f, HitStopFinisher = 0.24f, HitStopKill = 0.3f, ShakeIntensity = 14f, ShakeDuration = 0.24f, KnockbackForce = 200f, KnockbackUp = -70f, Damage = 2, FinisherDamage = 4, AttackSpeed = 0.14f, ComboWindow = 0.45f, ComboCooldown = 0.4f, DisplayName = "Club", Weight = 0.65f, ActiveTime = 0.14f },
        WeaponType.Hammer => new Stats { HitStopNormal = 0.18f, HitStopFinisher = 0.24f, HitStopKill = 0.36f, ShakeIntensity = 16f, ShakeDuration = 0.28f, KnockbackForce = 220f, KnockbackUp = -90f, Damage = 3, FinisherDamage = 5, AttackSpeed = 0.16f, ComboWindow = 0.5f, ComboCooldown = 0.45f, DisplayName = "Hammer", Weight = 0.7f, ActiveTime = 0.16f },
        WeaponType.GreatSword => new Stats { HitStopNormal = 0.21f, HitStopFinisher = 0.3f, HitStopKill = 0.45f, ShakeIntensity = 18f, ShakeDuration = 0.3f, KnockbackForce = 280f, KnockbackUp = -100f, Damage = 3, FinisherDamage = 5, AttackSpeed = 0.2f, ComboWindow = 0.6f, ComboCooldown = 0.5f, DisplayName = "Great Sword", Weight = 0.8f, ActiveTime = 0.18f },
        WeaponType.GreatClub => new Stats { HitStopNormal = 0.24f, HitStopFinisher = 0.36f, HitStopKill = 0.54f, ShakeIntensity = 20f, ShakeDuration = 0.36f, KnockbackForce = 350f, KnockbackUp = -120f, Damage = 4, FinisherDamage = 6, AttackSpeed = 0.22f, ComboWindow = 0.6f, ComboCooldown = 0.55f, DisplayName = "Great Club", Weight = 1.0f, ActiveTime = 0.2f },
        WeaponType.Sling => new Stats { HitStopNormal = 0.01f, HitStopFinisher = 0.02f, HitStopKill = 0.03f, ShakeIntensity = 2f, ShakeDuration = 0.05f, KnockbackForce = 60f, KnockbackUp = -30f, Damage = 1, FinisherDamage = 2, IsRanged = true, DisplayName = "Sling", Weight = 0.1f, ActiveTime = 0.01f },
        WeaponType.Bow => new Stats { HitStopNormal = 0.02f, HitStopFinisher = 0.03f, HitStopKill = 0.04f, ShakeIntensity = 3f, ShakeDuration = 0.06f, KnockbackForce = 80f, KnockbackUp = -40f, Damage = 2, FinisherDamage = 3, IsRanged = true, DisplayName = "Bow", Weight = 0.2f, ActiveTime = 0.01f },
        WeaponType.Gun => new Stats { HitStopNormal = 0.03f, HitStopFinisher = 0.05f, HitStopKill = 0.06f, ShakeIntensity = 5f, ShakeDuration = 0.08f, KnockbackForce = 100f, KnockbackUp = -50f, Damage = 3, FinisherDamage = 4, IsRanged = true, DisplayName = "Gun", Weight = 0.3f, ActiveTime = 0.01f },
        _ => new Stats { HitStopNormal = 0.03f, HitStopFinisher = 0.05f, HitStopKill = 0.06f, ShakeIntensity = 5f, ShakeDuration = 0.1f, KnockbackForce = 100f, KnockbackUp = -50f, Damage = 1, FinisherDamage = 2, DisplayName = "Unknown", Weight = 0f, ActiveTime = 0.01f }
    };
}
