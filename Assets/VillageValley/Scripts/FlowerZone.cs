using UnityEngine;

public class FlowerZone : MonoBehaviour
{
    public enum ZoneShape { Circle, Box }

    public ZoneShape shape = ZoneShape.Circle;
    [Min(1f)] public float radius = 35f;
    public Vector2 boxSize = new Vector2(70f, 70f);
    [Range(1, 10)] public int flowerType = 1;
    [Range(0f, 1f)] public float flowerChance = 0.9f;

    public bool Contains(Vector3 worldPosition)
    {
        Vector2 delta = new Vector2(worldPosition.x - transform.position.x, worldPosition.z - transform.position.z);
        if (shape == ZoneShape.Circle) return delta.sqrMagnitude <= radius * radius;
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(local.x) <= boxSize.x * 0.5f && Mathf.Abs(local.z) <= boxSize.y * 0.5f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.65f, 0.8f);
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
