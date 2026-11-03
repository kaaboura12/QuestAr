// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace InfimaGames.LowPolyShooterPack
{
    /// <summary>
    /// Weapon. This class handles most of the things that weapons need.
    /// </summary>
    public class Weapon : WeaponBehaviour
    {
        #region FIELDS SERIALIZED
        
        [Header("Firing")]

        [Tooltip("Is this weapon automatic? If yes, then holding down the firing button will continuously fire.")]
        [SerializeField] 
        private bool automatic;
        
        [Tooltip("How fast the projectiles are.")]
        [SerializeField]
        private float projectileImpulse = 400.0f;

        [Tooltip("Amount of shots this weapon can shoot in a minute. It determines how fast the weapon shoots.")]
        [SerializeField] 
        private int roundsPerMinutes = 200;

        [Tooltip("Mask of things recognized when firing.")]
        [SerializeField]
        private LayerMask mask;

        [Tooltip("Maximum distance at which this weapon can fire accurately. Shots beyond this distance will not use linetracing for accuracy.")]
        [SerializeField]
        private float maximumDistance = 500.0f;

        [Header("Animation")]

        [Tooltip("Transform that represents the weapon's ejection port, meaning the part of the weapon that casings shoot from.")]
        [SerializeField]
        private Transform socketEjection;

        [Header("Resources")]

        [Tooltip("Casing Prefab.")]
        [SerializeField]
        private GameObject prefabCasing;
        
        [Tooltip("Projectile Prefab. This is the prefab spawned when the weapon shoots.")]
        [SerializeField]
        private GameObject prefabProjectile;
        
        [Tooltip("The AnimatorController a player character needs to use while wielding this weapon.")]
        [SerializeField] 
        public RuntimeAnimatorController controller;

        [Tooltip("Weapon Body Texture.")]
        [SerializeField]
        private Sprite spriteBody;
        
        [Header("Audio Clips Holster")]

        [Tooltip("Holster Audio Clip.")]
        [SerializeField]
        private AudioClip audioClipHolster;

        [Tooltip("Unholster Audio Clip.")]
        [SerializeField]
        private AudioClip audioClipUnholster;
        
        [Header("Audio Clips Reloads")]

        [Tooltip("Reload Audio Clip.")]
        [SerializeField]
        private AudioClip audioClipReload;
        
        [Tooltip("Reload Empty Audio Clip.")]
        [SerializeField]
        private AudioClip audioClipReloadEmpty;
        
        [Header("Audio Clips Other")]

        [Tooltip("AudioClip played when this weapon is fired without any ammunition.")]
        [SerializeField]
        private AudioClip audioClipFireEmpty;

        #endregion

        #region FIELDS

        /// <summary>
        /// Weapon Animator.
        /// </summary>
        private Animator animator;
        /// <summary>
        /// Attachment Manager.
        /// </summary>
        private WeaponAttachmentManagerBehaviour attachmentManager;

        /// <summary>
        /// Amount of ammunition left.
        /// </summary>
        private int ammunitionCurrent;
        private bool ammunitionInitialized;

        #region Attachment Behaviours
        
        /// <summary>
        /// Equipped Magazine Reference.
        /// </summary>
        private MagazineBehaviour magazineBehaviour;
        /// <summary>
        /// Equipped Muzzle Reference.
        /// </summary>
        private MuzzleBehaviour muzzleBehaviour;

        #endregion

        /// <summary>
        /// The GameModeService used in this game!
        /// </summary>
        private IGameModeService gameModeService;
        /// <summary>
        /// The main player character behaviour component.
        /// </summary>
        private CharacterBehaviour characterBehaviour;

        /// <summary>
        /// The player character's camera.
        /// </summary>
        private Transform playerCamera;
        
        #endregion

        #region UNITY
        
        protected override void Awake()
        {
            //Get Animator.
            animator = GetComponent<Animator>();
            if (animator != null)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            //Get Attachment Manager.
            attachmentManager = GetComponent<WeaponAttachmentManagerBehaviour>();

            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
        }

        protected override void Start()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (animator != null && animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (attachmentManager == null)
                attachmentManager = GetComponent<WeaponAttachmentManagerBehaviour>();

            if (attachmentManager != null)
            {
                if (magazineBehaviour == null)
                    magazineBehaviour = attachmentManager.GetEquippedMagazine();
                if (muzzleBehaviour == null)
                    muzzleBehaviour = attachmentManager.GetEquippedMuzzle();
            }

            if (!ammunitionInitialized && magazineBehaviour != null)
            {
                ammunitionCurrent = magazineBehaviour.GetAmmunitionTotal();
                ammunitionInitialized = true;
            }

            if (gameModeService == null)
                gameModeService = ServiceLocator.Current.Get<IGameModeService>();
            if (characterBehaviour == null && gameModeService != null)
                characterBehaviour = gameModeService.GetPlayerCharacter();
            if (playerCamera == null && characterBehaviour != null && characterBehaviour.GetCameraWorld() != null)
                playerCamera = characterBehaviour.GetCameraWorld().transform;
            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main.transform;
        }

        #endregion

        #region GETTERS

        public override Animator GetAnimator() => animator;
        
        public override Sprite GetSpriteBody() => spriteBody;

        public override AudioClip GetAudioClipHolster() => audioClipHolster;
        public override AudioClip GetAudioClipUnholster() => audioClipUnholster;

        public override AudioClip GetAudioClipReload() => audioClipReload;
        public override AudioClip GetAudioClipReloadEmpty() => audioClipReloadEmpty;

        public override AudioClip GetAudioClipFireEmpty() => audioClipFireEmpty;
        
        public override AudioClip GetAudioClipFire()
        {
            EnsureReferences();
            return muzzleBehaviour != null ? muzzleBehaviour.GetAudioClipFire() : null;
        }
        
        public override int GetAmmunitionCurrent()
        {
            EnsureReferences();
            return ammunitionCurrent;
        }

        public override int GetAmmunitionTotal()
        {
            EnsureReferences();
            return magazineBehaviour != null ? magazineBehaviour.GetAmmunitionTotal() : 0;
        }

        public override bool IsAutomatic() => automatic;
        public override float GetRateOfFire() => roundsPerMinutes;
        
        public override bool IsFull()
        {
            EnsureReferences();
            return magazineBehaviour != null && ammunitionCurrent == magazineBehaviour.GetAmmunitionTotal();
        }

        public override bool HasAmmunition()
        {
            EnsureReferences();
            return ammunitionCurrent > 0;
        }

        public override RuntimeAnimatorController GetAnimatorController() => controller;
        public override WeaponAttachmentManagerBehaviour GetAttachmentManager() => attachmentManager;

        #endregion

        #region METHODS

        public override void Reload()
        {
            EnsureReferences();
            //Play Reload Animation.
            if (animator != null)
                animator.Play(HasAmmunition() ? "Reload" : "Reload Empty", 0, 0.0f);
        }
        public override void Fire(float spreadMultiplier = 1.0f)
        {
            EnsureReferences();

            //We need a muzzle in order to fire this weapon!
            if (muzzleBehaviour == null)
                return;
            
            //Make sure that we have a camera cached, otherwise we don't really have the ability to perform traces.
            if (playerCamera == null)
                return;

            //Get Muzzle Socket. This is the point we fire from.
            Transform muzzleSocket = muzzleBehaviour.GetSocket();
            
            //Play the firing animation.
            const string stateName = "Fire";
            if (animator != null)
                animator.Play(stateName, 0, 0.0f);
            //Reduce ammunition! We just shot, so we need to get rid of one!
            ammunitionCurrent = Mathf.Clamp(ammunitionCurrent - 1, 0, GetAmmunitionTotal());

            //Play all muzzle effects.
            muzzleBehaviour.Effect();
            
            //Determine the rotation that we want to shoot our projectile in.
            Quaternion rotation = Quaternion.LookRotation(playerCamera.forward * 1000.0f - muzzleSocket.position);
            
            //If there's something blocking, then we can aim directly at that thing, which will result in more accurate shooting.
            if (Physics.Raycast(new Ray(playerCamera.position, playerCamera.forward),
                out RaycastHit hit, maximumDistance, mask))
                rotation = Quaternion.LookRotation(hit.point - muzzleSocket.position);
                
            //Spawn projectile from the projectile spawn point.
            GameObject projectile = Instantiate(prefabProjectile, muzzleSocket.position, rotation);
            //Add velocity to the projectile.
            projectile.GetComponent<Rigidbody>().linearVelocity = projectile.transform.forward * projectileImpulse;   
        }

        public override void FillAmmunition(int amount)
        {
            EnsureReferences();
            if (magazineBehaviour == null)
                return;

            //Update the value by a certain amount.
            ammunitionCurrent = amount != 0 ? Mathf.Clamp(ammunitionCurrent + amount, 
                0, GetAmmunitionTotal()) : magazineBehaviour.GetAmmunitionTotal();
        }

        public override void EjectCasing()
        {
            //Spawn casing prefab at spawn point.
            if(prefabCasing != null && socketEjection != null)
                Instantiate(prefabCasing, socketEjection.position, socketEjection.rotation);
        }

        #endregion
    }
}
