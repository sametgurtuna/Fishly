using UnityEngine;
using UnityEngine.UI;

// Bu script'i Canvas'ın içindeki boş bir GameObject'e veya bir "UIManager" objesine atabilirsin.
public class MoveUIElementsUpDown : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Bu diziye, taşınmasını istediğin tüm UI elemanlarını (Panel, Image, Button vb.) sürükle.")]
    public RectTransform[] elementsToMove;

    [Header("Hareket Miktarı")]
    [Tooltip("Her tıklamada elemanların ne kadar hareket edeceğini belirler (piksel cinsinden).")]
    public float moveAmount = 1f;

    // Yukarı hareket fonksiyonu
    // Butonun OnClick event'inden çağırabilmek için 'public' olmalı.
    public void MoveUp()
    {
        // Diziye herhangi bir eleman atanıp atanmadığını kontrol edelim.
        if (elementsToMove == null || elementsToMove.Length == 0)
        {
            Debug.LogWarning("Taşınacak eleman atanmamış! Lütfen Inspector'dan 'Elements To Move' dizisine eleman ekleyin.");
            return; // Eleman yoksa fonksiyondan çık.
        }

        // Dizideki her bir eleman için döngü başlat.
        foreach (RectTransform element in elementsToMove)
        {
            // Null kontrolü yap
            if (element == null) continue;

            // Elemanın mevcut pozisyonunu al.
            Vector2 currentPosition = element.anchoredPosition;

            // Mevcut pozisyonun Y değerini artır.
            currentPosition.y += moveAmount;

            // Hesaplanmış yeni pozisyonu elemana uygula.
            element.anchoredPosition = currentPosition;
        }
    }

    // Aşağı hareket fonksiyonu
    // Butonun OnClick event'inden çağırabilmek için 'public' olmalı.
    public void MoveDown()
    {
        // Diziye herhangi bir eleman atanıp atanmadığını kontrol edelim.
        if (elementsToMove == null || elementsToMove.Length == 0)
        {
            Debug.LogWarning("Taşınacak eleman atanmamış! Lütfen Inspector'dan 'Elements To Move' dizisine eleman ekleyin.");
            return; // Eleman yoksa fonksiyondan çık.
        }

        // Dizideki her bir eleman için döngü başlat.
        foreach (RectTransform element in elementsToMove)
        {
            // Null kontrolü yap
            if (element == null) continue;

            // Elemanın mevcut pozisyonunu al.
            Vector2 currentPosition = element.anchoredPosition;

            // Mevcut pozisyonun Y değerini azalt.
            currentPosition.y -= moveAmount;

            // Hesaplanmış yeni pozisyonu elemana uygula.
            element.anchoredPosition = currentPosition;
        }
    }
}