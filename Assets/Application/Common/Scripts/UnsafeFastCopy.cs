using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// native container関連
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public class UnsafeFastCopy  {

    public static void Copy(NativeArray<Matrix4x4> nativeArray, Matrix4x4[] arrays, int srcIndex, int destIndex, int length)
    {
        unsafe
        {
            Matrix4x4* src = (Matrix4x4*)nativeArray.GetUnsafeReadOnlyPtr();
            src = src + srcIndex;
            fixed (Matrix4x4* dest = &arrays[destIndex])
            {
                UnsafeUtility.MemCpy(dest, src, sizeof(Matrix4x4) * length);
            }
        }
    }
    public static void Copy(NativeArray<Vector4> nativeArray, Vector4[] arrays, int srcIndex, int destIndex, int length)
    {
        unsafe
        {
            Vector4* src = (Vector4*)nativeArray.GetUnsafeReadOnlyPtr();
            src = src + srcIndex;
            fixed (Vector4* dest = &arrays[destIndex])
            {
                UnsafeUtility.MemCpy(dest, src, sizeof(Vector4) * length);
            }
        }
    }
}
