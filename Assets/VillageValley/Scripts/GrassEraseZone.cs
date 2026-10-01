using UnityEngine;

public class GrassEraseZone : MonoBehaviour
{
    public enum ZoneShape { Circle, Box }

    public ZoneShape shape = ZoneShape.Box;
    [Min(1f)] public float radius = 25f;
    public Vector2 boxSize = new Vector2(50f, 50f);

    public bool Contains(Vector3 worldPosition)
    {
        Vector2 delta = new Vector2(worldPosition.x - transform.position.x, worldPosition.z - transform.position.z);
        if (shape == ZoneShape.Circle) return delta.sqrMagnitude <= radius * radius;
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(local.x) <= boxSize.x * 0.5f && Mathf.Abs(local.z) <= boxSize.y * 0.5f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.12f, 0.08f, 0.85f);
        if (shape == ZoneShape.Circle) DrawCircle(transform.position, radius);
        else
        {
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxSize.x, 2f, boxSize.y));
            Gizmos.matrix = old;
        }
    }

    private static void DrawCircle(Vector3 center, float size)
    {
        Vector3 previous = center + Vector3.right * size;
        for (int i = 1; i <= 48; i++)
        {
            float angle = i / 48f * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * size;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
