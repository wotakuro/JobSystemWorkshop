using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class TransparentAssetPipe : RenderPipelineAsset
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("SRP-Demo/Create")]
    static void CreateBasicAssetPipeline()
    {
        var instance = ScriptableObject.CreateInstance<TransparentAssetPipe>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/Application/Datas/SRP/TransparentAssetPipe.asset");
    }
#endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new TransparentAssetPipeInstance();
    }
}

public class TransparentAssetPipeInstance : RenderPipeline
{

    private CullResults cull;
    private ScriptableCullingParameters cullingParams;
    private CommandBuffer cmd;
    private ShaderPassName zPrepass = new ShaderPassName("ZPrepass");
    private ShaderPassName basicPass = new ShaderPassName("BasicPass");

    public override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        base.Render(context, cameras);
        if (cmd == null)
        {
            cmd = new CommandBuffer();
        }

        foreach (var camera in cameras)
        {
            // Culling
            if (!CullResults.GetCullingParameters(camera, out cullingParams))
                continue;
            cullingParams.cullingFlags = 0;

            UnityEngine.Profiling.Profiler.BeginSample("Culling Execute");
            CullResults.Cull(ref cullingParams, context,ref cull);
            UnityEngine.Profiling.Profiler.EndSample();

            // Setup camera for rendering (sets render target, view/projection matrices and other
            // per-camera built-in shader variables).
            context.SetupCameraProperties(camera);

            // clear depth buffer
            cmd.Clear();
            cmd.ClearRenderTarget(true, true, Color.black,1.0f);
            context.ExecuteCommandBuffer(cmd);

            // Draw opaque objects using BasicPass shader pass
            var settings = new DrawRendererSettings(camera, zPrepass);
            settings.sorting.flags = SortFlags.CommonOpaque;

            var filterSettings = new FilterRenderersSettings(true) { renderQueueRange = RenderQueueRange.opaque };
            context.DrawRenderers(cull.visibleRenderers, ref settings, filterSettings);

            // Draw skybox

            // Draw transparent objects using BasicPass shader pass
            settings.sorting.flags = SortFlags.CommonOpaque;
            filterSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cull.visibleRenderers, ref settings, filterSettings);

            settings = new DrawRendererSettings(camera, basicPass);
            settings.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref settings, filterSettings);
            
            context.Submit();
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        if (cmd != null)
        {
            cmd.Release();
            cmd = null;
        }
    }
}
