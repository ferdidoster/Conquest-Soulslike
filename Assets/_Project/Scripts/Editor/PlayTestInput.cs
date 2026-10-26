using UnityEditor;
using UnityEngine.InputSystem;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Lets virtual input devices drive play mode while the editor sits in the background, for the
    /// marker-file play tests, and puts the project's settings back afterwards.
    /// </summary>
    internal sealed class PlayTestInput
    {
        private readonly InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private readonly InputSettings.BackgroundBehavior _backgroundBehavior;
        private readonly bool _runInBackground;

        public PlayTestInput()
        {
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            _runInBackground = PlayerSettings.runInBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            PlayerSettings.runInBackground = true;
        }

        public void Restore()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
            PlayerSettings.runInBackground = _runInBackground;
        }
    }
}
