using System.Numerics;

namespace Minecraftonia.VoxelRendering;

public readonly struct VoxelCamera
{
    public VoxelCamera(Vector3 forward, Vector3 right, Vector3 up, float tanHalfFov, float aspect)
    {
        Forward = forward;
        Right = right;
        Up = up;
        TanHalfFov = tanHalfFov;
        Aspect = aspect;
    }

    public Vector3 Forward { get; }
    public Vector3 Right { get; }
    public Vector3 Up { get; }
    public float TanHalfFov { get; }
    public float Aspect { get; }
}
