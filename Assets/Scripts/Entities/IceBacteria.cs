using UnityEngine;
using MobileGame.Core;
using MobileGame.Utilities;

namespace MobileGame.Entities
{
    public enum IceBacteriumState
    {
        Chasing,
        TelegraphingShoot
    }

    /// <summary>
    /// Batterio di Ghiaccio (Wave 6+):
    /// Insegue il player e spara a intervalli un dardo congelante in una delle 4 direzioni cardinali.
    /// Dotato di 2 frame di animazione di movimento e 4 frame direzionali di sparo.
    /// Zero allocazioni GC nei loop (AGENTS.md §4).
    /// </summary>
    public class IceBacteria : MicrobeEnemy
    {
        [Header("Comportamento Ghiaccio")]
        [SerializeField] private float _shootCooldown  = 1.4f;   // Spara molto più spesso
        [SerializeField] private float _shootRange     = 8.5f;
        [SerializeField] private float _windupDuration = 0.22f;  // Mira e carica più rapida
        [SerializeField] private float _baseHealth     = 60f;    // Più vita di microbi (30) e virus (20)

        // Sprites cachati in Awake
        private Sprite _spriteMove1;
        private Sprite _spriteMove2;
        private Sprite _spriteShootUp;
        private Sprite _spriteShootDown;
        private Sprite _spriteShootLeft;
        private Sprite _spriteShootRight;

        private IceBacteriumState _state = IceBacteriumState.Chasing;

        private float   _shootTimer;
        private float   _stateTimer;
        private float   _animTimer;
        private int     _animFrame;
        private Vector2 _targetDirection;
        private float   _projectileDamage = 8f;

        // Pool condiviso per i proiettili criogenici (zero GC)
        private static ObjectPool<IceProjectile> s_projPool;
        private static System.Action<IceProjectile> s_returnProjCallback;
        private static Transform s_projRoot;

        protected override void Awake()
        {
            base.Awake();
            _sr = GetComponent<SpriteRenderer>();

            gameObject.tag = "Enemy";
            gameObject.layer = 3; // Layer Enemy
            transform.localScale = new Vector3(0.30f, 0.30f, 1f); // Proporzione compatta scalata
            _initialLocalScale = transform.localScale;

            var col = GetComponent<CircleCollider2D>();
            if (col != null) col.radius = 0.30f;

            // Statistiche default per il nemico di ghiaccio
            _baseSpeed         = 1.9f;
            _contactDamage     = 10f;
            _baseContactDamage = 10f;
            _baseMaxHealth     = _baseHealth;
            _scoreValue        = 25;
            _xpGemsDropCount   = 2;

            LoadSprites();
            EnsureProjectilePool();
        }

        private void LoadSprites()
        {
            _spriteMove1      = Resources.Load<Sprite>("Sprites/ice_bacteria_1");
            _spriteMove2      = Resources.Load<Sprite>("Sprites/ice_bacteria_2");
            _spriteShootUp    = Resources.Load<Sprite>("Sprites/ice_bacteria_shoot_up");
            _spriteShootDown  = Resources.Load<Sprite>("Sprites/ice_bacteria_shoot_down");
            _spriteShootLeft  = Resources.Load<Sprite>("Sprites/ice_bacteria_shoot_left");
            _spriteShootRight = Resources.Load<Sprite>("Sprites/ice_bacteria_shoot_right");

            if (_sr != null && _spriteMove1 != null)
            {
                _sr.sprite = _spriteMove1;
            }
        }

        private static void EnsureProjectilePool()
        {
            if (s_projPool != null) return;

            if (s_projRoot == null)
            {
                var go = new GameObject("SharedIceProjectilePool");
                Object.DontDestroyOnLoad(go);
                s_projRoot = go.transform;
            }

            s_returnProjCallback = ReturnProjectile;

            s_projPool = new ObjectPool<IceProjectile>(
                factory: () =>
                {
                    var projGO = new GameObject("IceProjectile");
                    projGO.transform.SetParent(s_projRoot);

                    var sr = projGO.AddComponent<SpriteRenderer>();
                    sr.sprite = Resources.Load<Sprite>("Sprites/ice_projectile");
                    sr.sortingOrder = 4; // Sopra al terreno e nemici

                    projGO.AddComponent<Rigidbody2D>();
                    projGO.AddComponent<CircleCollider2D>();
                    var proj = projGO.AddComponent<IceProjectile>();
                    projGO.SetActive(false);
                    return proj;
                },
                onGet: p => p.gameObject.SetActive(true),
                onRelease: p =>
                {
                    p.gameObject.SetActive(false);
                    p.transform.SetParent(s_projRoot);
                },
                initialCapacity: 16
            );
        }

        private static void ReturnProjectile(IceProjectile p)
        {
            s_projPool?.Release(p);
        }

        public override void Initialize(Transform player, int wave, System.Action<MicrobeEnemy> onDied)
        {
            base.Initialize(player, wave, onDied);
            transform.localScale = new Vector3(0.42f, 0.42f, 1f);

            // Danno proiettile criogenico scalato per wave (+12% per wave)
            _projectileDamage = 8f * (1f + (wave - 1) * 0.12f);

            _state       = IceBacteriumState.Chasing;
            _shootTimer  = Random.Range(0.2f, 0.7f); // Inizia a sparare quasi subito dopo lo spawn
            _stateTimer  = 0f;
            _animTimer   = 0f;
            _animFrame   = 0;

            if (_sr != null && _spriteMove1 != null)
            {
                _sr.sprite = _spriteMove1;
            }
        }

        protected override void Update()
        {
            base.Update(); // Gestione Hit Flash e Sgretolamento

            if (_isDying || _playerTransform == null) return;
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;

            // Animazione ciclica dei 2 frame di movimento
            if (_state == IceBacteriumState.Chasing)
            {
                _animTimer += Time.deltaTime;
                if (_animTimer >= 0.28f)
                {
                    _animTimer = 0f;
                    _animFrame = (_animFrame + 1) % 2;
                    if (_sr != null)
                    {
                        _sr.sprite = (_animFrame == 0 && _spriteMove1 != null) ? _spriteMove1 : _spriteMove2;
                    }
                }
            }
        }

        protected override void FixedUpdate()
        {
            if (_playerTransform == null || _isDying) return;

            Vector2 toPlayer = (Vector2)_playerTransform.position - _rb.position;
            float sqrDist = toPlayer.sqrMagnitude;

            switch (_state)
            {
                case IceBacteriumState.Chasing:
                    // Inseguimento
                    Vector2 dir = toPlayer.normalized;
                    Vector2 next = _rb.position + dir * (_currentSpeed * Time.fixedDeltaTime);
                    if (ArenaBoundary.Instance != null)
                    {
                        next = ArenaBoundary.Instance.ClampPosition(next, 0.4f);
                    }
                    _rb.MovePosition(next);

                    // Cadenza di sparo se entro il raggio
                    _shootTimer += Time.fixedDeltaTime;
                    if (_shootTimer >= _shootCooldown && sqrDist <= (_shootRange * _shootRange))
                    {
                        StartTelegraph(toPlayer);
                    }
                    break;

                case IceBacteriumState.TelegraphingShoot:
                    // Si ferma brevemente per mirare e sparare
                    _stateTimer += Time.fixedDeltaTime;
                    if (_stateTimer >= _windupDuration)
                    {
                        ShootProjectile();
                    }
                    break;
            }
        }

        private void StartTelegraph(Vector2 toPlayer)
        {
            _state          = IceBacteriumState.TelegraphingShoot;
            _stateTimer     = 0f;
            _targetDirection = toPlayer.normalized;

            // Determina la direzione cardinale predominante e imposta il rispettivo sprite
            float absX = Mathf.Abs(toPlayer.x);
            float absY = Mathf.Abs(toPlayer.y);

            if (_sr != null)
            {
                if (absX >= absY)
                {
                    if (toPlayer.x >= 0f)
                    {
                        if (_spriteShootRight != null) _sr.sprite = _spriteShootRight;
                    }
                    else
                    {
                        if (_spriteShootLeft != null) _sr.sprite = _spriteShootLeft;
                    }
                }
                else
                {
                    if (toPlayer.y >= 0f)
                    {
                        if (_spriteShootUp != null) _sr.sprite = _spriteShootUp;
                    }
                    else
                    {
                        if (_spriteShootDown != null) _sr.sprite = _spriteShootDown;
                    }
                }
            }
        }

        private void ShootProjectile()
        {
            if (s_projPool != null)
            {
                IceProjectile proj = s_projPool.Get();
                Vector2 launchDir = _targetDirection;
                if (_playerTransform != null)
                {
                    // Aggiorna mira al momento dello sparo
                    Vector2 toPlayer = (Vector2)_playerTransform.position - _rb.position;
                    if (toPlayer.sqrMagnitude > 0.001f) launchDir = toPlayer.normalized;
                }
                proj.Launch(_rb.position, launchDir, s_returnProjCallback, _projectileDamage);
            }

            // Ripristina stato di inseguimento
            _state      = IceBacteriumState.Chasing;
            _shootTimer = 0f;
            _animTimer  = 0f;
            if (_sr != null && _spriteMove1 != null)
            {
                _sr.sprite = _spriteMove1;
            }
        }

        protected override Color GetThemeColor() => new Color(0.15f, 0.85f, 1f); // Ciano ghiaccio
    }
}
