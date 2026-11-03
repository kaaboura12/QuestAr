// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace InfimaGames.LowPolyShooterPack.Interface
{
    /// <summary>
    /// Interface Element.
    /// </summary>
    public abstract class Element : MonoBehaviour
    {
        #region FIELDS
        
        /// <summary>
        /// Game Mode Service.
        /// </summary>
        protected IGameModeService gameModeService;
        
        /// <summary>
        /// Player Character.
        /// </summary>
        protected CharacterBehaviour playerCharacter;
        /// <summary>
        /// Player Character Inventory.
        /// </summary>
        protected InventoryBehaviour playerCharacterInventory;

        /// <summary>
        /// Equipped Weapon.
        /// </summary>
        protected WeaponBehaviour equippedWeapon;
        
        #endregion

        #region UNITY

        /// <summary>
        /// Awake.
        /// </summary>
        protected virtual void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (gameModeService == null)
                gameModeService = ServiceLocator.Current.Get<IGameModeService>();
            if (playerCharacter == null && gameModeService != null)
                playerCharacter = gameModeService.GetPlayerCharacter();
            if (playerCharacterInventory == null && playerCharacter != null)
                playerCharacterInventory = playerCharacter.GetInventory();
        }
        
        /// <summary>
        /// Update.
        /// </summary>
        private void Update()
        {
            EnsureReferences();

            //Ignore if we don't have an Inventory.
            if (playerCharacterInventory == null)
                return;

            //Get Equipped Weapon.
            equippedWeapon = playerCharacterInventory.GetEquipped();
            if (equippedWeapon == null)
                return;
            
            //Tick.
            Tick();
        }

        #endregion

        #region METHODS

        /// <summary>
        /// Tick.
        /// </summary>
        protected virtual void Tick() {}

        #endregion
    }
}
