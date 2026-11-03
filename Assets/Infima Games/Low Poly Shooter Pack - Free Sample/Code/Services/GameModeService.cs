// Copyright 2021, Infima Games. All Rights Reserved.

namespace InfimaGames.LowPolyShooterPack
{
    /// <summary>
    /// Game Mode Service.
    /// </summary>
    public class GameModeService : IGameModeService
    {
        #region FIELDS
        
        /// <summary>
        /// The Player Character.
        /// </summary>
        private CharacterBehaviour playerCharacter;
        
        #endregion
        
        #region FUNCTIONS
        
        public CharacterBehaviour GetPlayerCharacter()
        {
            //Make sure we have a player character that is good to go!
            if (playerCharacter == null)
            {
                CharacterBehaviour[] allCharacters = UnityEngine.Object.FindObjectsByType<CharacterBehaviour>();
                for (int i = 0; i < allCharacters.Length; i++)
                {
                    if (allCharacters[i] != null && allCharacters[i].GetType().Name == "ARInfimaPlayerCharacter")
                    {
                        playerCharacter = allCharacters[i];
                        break;
                    }
                }

                if (playerCharacter == null && allCharacters.Length > 0)
                    playerCharacter = allCharacters[0];
            }
            
            //Return.
            return playerCharacter;
        }
        
        #endregion
    }
}
