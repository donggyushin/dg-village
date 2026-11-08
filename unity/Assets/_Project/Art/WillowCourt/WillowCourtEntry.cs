using UnityEngine;

// Match the normal PlayerSpawner's Start-time activation without requiring a login.
// InputSystem initializes the project-wide action asset before Start.
public sealed class WillowCourtEntry : MonoBehaviour
{
    [SerializeField] private GameObject _player;
    [SerializeField] private GameObject _editorPreview;

    private void Start()
    {
        if (_editorPreview) _editorPreview.SetActive(false);
        if (_player) _player.SetActive(true);
    }
}
