using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerCardUI : MonoBehaviour
{
    [Header("Referencias")]
    public Image avatarImage;
    public TextMeshProUGUI playerNameText;
    public GameObject readyIndicator;    // panel verde "LISTO"
    public GameObject notReadyIndicator; // panel gris "ESPERANDO"
    public GameObject hostIcon;          // estrella o corona de host

    public void Configure(string name, bool ready, bool isHost, Texture2D avatar)
    {
        if (playerNameText != null) playerNameText.text = name;
        if (readyIndicator != null) readyIndicator.SetActive(ready);
        if (notReadyIndicator != null) notReadyIndicator.SetActive(!ready);
        if (hostIcon != null) hostIcon.SetActive(isHost);
        
        if (avatarImage != null && avatar != null)
        {
            avatarImage.sprite = Sprite.Create(avatar, new Rect(0, 0, avatar.width, avatar.height), new Vector2(0.5f, 0.5f));
        }
    }

    public void SetReady(bool ready)
    {
        if (readyIndicator != null) readyIndicator.SetActive(ready);
        if (notReadyIndicator != null) notReadyIndicator.SetActive(!ready);
    }
}
