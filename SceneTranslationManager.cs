using MSCLoader;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Manages scene transitions and translation flags
    /// Tracks whether supported scenes have already received their initial translation pass
    /// </summary>
    public class SceneTranslationManager
    {
        // Scene translation states
        private bool hasTranslatedMainMenu = false;
        private bool hasTranslatedGameScene = false;
        
        // Current scene tracking
        private string currentScene = "";
        private string previousScene = "";

        public SceneTranslationManager()
        {
        }

        /// <summary>
        /// Check if a scene needs translation
        /// </summary>
        public bool ShouldTranslateScene(string sceneName)
        {
            switch (sceneName)
            {
                case "MainMenu":
                    return !hasTranslatedMainMenu;
                    
                case "GAME":
                    return !hasTranslatedGameScene;
                    
                default:
                    return false;
            }
        }

        /// <summary>
        /// Mark a scene as translated
        /// </summary>
        public void MarkSceneTranslated(string sceneName)
        {
            switch (sceneName)
            {
                case "MainMenu":
                    hasTranslatedMainMenu = true;
                    CoreConsole.Print("[SceneTranslationManager] Main Menu marked as translated");
                    break;
                    
                case "GAME":
                    hasTranslatedGameScene = true;
                    CoreConsole.Print("[SceneTranslationManager] Game scene marked as translated");
                    break;
            }
        }

        /// <summary>
        /// Update scene tracking and handle scene changes
        /// Returns true if scene changed
        /// </summary>
        public bool UpdateScene(string newScene)
        {
            if (string.IsNullOrEmpty(newScene))
                return false;

            if (currentScene != newScene)
            {
                previousScene = currentScene;
                currentScene = newScene;
                
                HandleSceneChange(previousScene, currentScene);
                
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Handle scene-specific cleanup/reset logic
        /// </summary>
        private void HandleSceneChange(string from, string to)
        {
            if (to == "MainMenu")
            {
                // Reset game scene when returning to menu
                hasTranslatedGameScene = false;
            }
            else if (to == "GAME")
            {
                // Reset main menu when entering game
                hasTranslatedMainMenu = false;
            }
        }

        /// <summary>
        /// Reset all translation flags (for F8 reload)
        /// </summary>
        public void ResetAll()
        {
            hasTranslatedMainMenu = false;
            hasTranslatedGameScene = false;
        }

        /// <summary>
        /// Check if scene has been translated
        /// </summary>
        public bool HasSceneBeenTranslated(string sceneName)
        {
            switch (sceneName)
            {
                case "MainMenu":
                    return hasTranslatedMainMenu;
                    
                case "GAME":
                    return hasTranslatedGameScene;
                    
                default:
                    return false;
            }
        }
    }
}
