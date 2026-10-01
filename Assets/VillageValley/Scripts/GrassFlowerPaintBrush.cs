using System;
using System.Collections.Generic;
using UnityEngine;

public class GrassFlowerPaintBrush : MonoBehaviour
{
    public enum BrushMode { PaintFlowers, EraseGrassAndFlowers }

    [Serializable]
    public struct Stroke
    {
        public Vector3 position;
        public float radius;
        public int flowerType;
        public bool erase;
    }

    public Terrain terrain;
    public GrassFlowerField field;
    public BrushMode mode = BrushMode.PaintFlowers;
    [Range(1, 10)] public int flowerType = 1;
    [Min(1f)] public float brushRadius = 18f;
    [Range(0.05f, 1f)] public float brushSpacing = 0.35f;
    public List<Stroke> strokes = new List<Stroke>();

    public void AddStroke(Vector3 position)
    {
        strokes.Add(new Stroke
        {
            position = position,
            radius = brushRadius,
            flowerType = Mathf.Clamp(flowerType, 1, 10),
            erase = mode == BrushMode.EraseGrassAndFlowers
        });
    }

    public bool TryEvaluate(Vector3 position, out bool erase, out int paintedFlowerType)
    {
        erase = false;
        paintedFlowerType = 0;
        for (int i = strokes.Count - 1; i >= 0; i--)
        {
            Stroke stroke = strokes[i];
            Vector2 delta = new Vector2(position.x - stroke.position.x, position.z - stroke.position.z);
            if (delta.sqrMagnitude > stroke.radius * stroke.radius) continue;
            erase = stroke.erase;
            paintedFlowerType = stroke.erase ? 0 : Mathf.Clamp(stroke.flowerType, 1, 10);
            return true;
        }
        return false;
    }

    public void ClearAllPaint() { strokes.Clear(); }
}
