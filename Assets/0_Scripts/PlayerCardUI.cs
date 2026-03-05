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

    public void SetReady(bool ready)
    {
        readyIndicator.SetActive(ready);
        notReadyIndicator.SetActive(!ready);
    }
}