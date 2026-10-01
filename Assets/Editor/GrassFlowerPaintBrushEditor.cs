using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrassFlowerPaintBrush))]
public class GrassFlowerPaintBrushEditor : Editor
{
    private Vector3 lastPaintPosition;
    private bool hasLastPaintPosition;

    public override void OnInspectorGUI()
    {
        GrassFlowerPaintBrush brush = (GrassFlowerPaintBrush)target;
        EditorGUILayout.HelpBox("Scene 뷰에서 Terrain 위를 클릭하거나 드래그하세요. Paint Flowers는 선택한 꽃을 칠하고, Erase는 풀과 꽃을 지웁니다.", MessageType.Info);
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("Rebuild Grass Field")) Rebuild(brush);
        if (GUILayout.Button("Clear All Painted Strokes"))
        {
            Undo.RecordObject(brush, "Clear Grass Flower Paint");
            brush.ClearAllPaint();
            EditorUtility.SetDirty(brush);
            Rebuild(brush);
        }
        EditorGUILayout.LabelField("Painted Strokes", brush.strokes.Count.ToString());
    }

    private void OnSceneGUI()
    {
        GrassFlowerPaintBrush brush = (GrassFlowerPaintBrush)target;
        if (brush.terrain == null) return;
        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!brush.terrain.GetComponent<TerrainCollider>().Raycast(ray, out RaycastHit hit, 100000f)) return;

        Color color = brush.mode == GrassFlowerPaintBrush.BrushMode.PaintFlowers
            ? new Color(1f, 0.2f, 0.65f, 0.95f)
            : new Color(1f, 0.1f, 0.05f, 0.95f);
        Handles.color = color;
        Handles.DrawWireDisc(hit.point, hit.normal, brush.brushRadius);
        Handles.color = new Color(color.r, color.g, color.b, 0.12f);
        Handles.DrawSolidDisc(hit.point, hit.normal, brush.brushRadius);
        SceneView.RepaintAll();

        bool paintEvent = e.button == 0 && !e.alt && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
        if (!paintEvent) { if (e.type == EventType.MouseUp) hasLastPaintPosition = false; return; }
        float requiredDistance = brush.brushRadius * brush.brushSpacing;
        if (hasLastPaintPosition && Vector3.Distance(lastPaintPosition, hit.point) < requiredDistance) { e.Use(); return; }

        Undo.RecordObject(brush, brush.mode == GrassFlowerPaintBrush.BrushMode.PaintFlowers ? "Paint Flowers" : "Erase Grass");
        brush.AddStroke(hit.point);
        EditorUtility.SetDirty(brush);
        lastPaintPosition = hit.point;
        hasLastPaintPosition = true;
        e.Use();

        // Rebuild after every stroke so the result is visible while dragging.
        Rebuild(brush);
    }

    private static void Rebuild(GrassFlowerPaintBrush brush)
    {
        if (brush.field != null)
        {
            brush.field.Rebuild();
            EditorUtility.SetDirty(brush.field);
        }
        EditorUtility.SetDirty(brush);
        if (brush.gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(brush.gameObject.scene);
        SceneView.RepaintAll();
    }
}
