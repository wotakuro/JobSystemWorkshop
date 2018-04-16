using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class MyScriptableRenderPipeline : RenderPipelineAsset
{
    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new MyScriptableRenderPipelineInstance();
    }
}

/// ���̃v���W�F�N�g�����ɍ쐬���� ScriptableRenderPipeline( SRP )�ł�
public class MyScriptableRenderPipelineInstance : RenderPipeline
{
    private CullResults cull;
    private ScriptableCullingParameters cullingParams;
    private CommandBuffer cmd;
    private ShaderPassName zPrepass = new ShaderPassName("ZPrepass");
    private ShaderPassName basicPass = new ShaderPassName("BasicPass");

    ///
    public List <CommandBuffer> zPrepassCommandBuffers = new List<CommandBuffer>();
    public List<CommandBuffer> actualCommandBuffers = new List<CommandBuffer>();

    private static MyScriptableRenderPipelineInstance instance;

    public static MyScriptableRenderPipelineInstance Instance
    {
        get { return instance; }
    }

    /// <summary>
    /// �R���X�g���N�^
    /// </summary>
    public MyScriptableRenderPipelineInstance()
    {
        instance = this;
    }

    /// <summary>
    /// ���ۂ̕`�揈��
    /// </summary>
    public override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        base.Render(context, cameras);
        if (cmd == null)
        {
            cmd = new CommandBuffer();
        }
        int idx = 0;
        foreach (var camera in cameras)
        {
            // Culling���܂�
            if (!CullResults.GetCullingParameters(camera, out cullingParams))
                continue;
            CullResults.Cull(ref cullingParams, context,ref cull);

            // �J�����Ɋւ���Shader�p�����[�^�[���Z�b�g�A�b�v���܂�
            context.SetupCameraProperties(camera);

            // ��ʂ��N���A���܂�
            cmd.Clear();
            cmd.ClearRenderTarget(true, true, Color.black,1.0f);
            context.ExecuteCommandBuffer(cmd);

            // Directional Light�̒l��ݒ肵�܂�
            SetUpDirectionalLightParam(cull.visibleLights);

            // ����CommandBuffer�ɓ����Ă���΁c
            if (zPrepassCommandBuffers != null && idx == 0) {
                ExecuteCommandBufferList(context,zPrepassCommandBuffers);
            }
            // �L�����N�^�[���@ZPrepass�ŕ`�悵�܂�
            DrawCharacter(context, camera, zPrepass, SortFlags.CommonOpaque);
            // BG��BasicPass�ŕ`�悵�܂�
            DrawBg(context, camera);

            // ����CommandBuffer�ɓ����Ă���΁c
            if (actualCommandBuffers != null && idx == 0) {
                ExecuteCommandBufferList(context, actualCommandBuffers);
            }
            // �L�����N�^�[��BasicPass�ŕ`�悵�܂�
            DrawCharacter(context, camera, basicPass, SortFlags.OptimizeStateChanges);
            // �Ō�ɉe��`�悵�܂�
            DrawShadow(context, camera);

            // �`����e���R�~�b�g���܂�
            context.Submit();
            ++idx;
        }
    }

    private void ExecuteCommandBufferList(ScriptableRenderContext context, List<CommandBuffer> cmdBuffers)
    {
        if (cmdBuffers == null) { return; }
        foreach (var cmdBuffer in cmdBuffers)
        {
            context.ExecuteCommandBuffer(cmdBuffer);
        }
    }


    // �w�肳�ꂽ�@Pass�ŃL�����N�^�[��`�悵�܂�
    private void DrawCharacter(ScriptableRenderContext context, Camera camera, ShaderPassName pass,SortFlags sortFlags)
    {
        var settings = new DrawRendererSettings(camera, pass);
        settings.sorting.flags = sortFlags;

        var filterSettings = new FilterRenderersSettings(true)
        {
            renderQueueRange = RenderQueueRange.transparent,
            layerMask = 1 << LayerDefine.CHARA
        };
        context.DrawRenderers(cull.visibleRenderers, ref settings, filterSettings);
    }

    // �w�肳�ꂽ���e��BG��`�悵�܂�
    private void DrawBg(ScriptableRenderContext context, Camera camera)
    {
        var settings = new DrawRendererSettings(camera, basicPass);
        settings.sorting.flags = SortFlags.CommonOpaque;

        var filterSettings = new FilterRenderersSettings(true)
        {
            renderQueueRange = RenderQueueRange.opaque,
            layerMask = 1 << LayerDefine.BG
        };
        context.DrawRenderers(cull.visibleRenderers, ref settings, filterSettings);
    }
    // �w�肳�ꂽ���e�ŉe��`�悵�܂�
    private void DrawShadow(ScriptableRenderContext context, Camera camera)
    {
        var settings = new DrawRendererSettings(camera, basicPass);
        settings.sorting.flags = SortFlags.CommonTransparent;

        var filterSettings = new FilterRenderersSettings(true)
        {
            renderQueueRange = RenderQueueRange.transparent,
            layerMask = 1 << LayerDefine.SHADOW
        };
        context.DrawRenderers(cull.visibleRenderers, ref settings, filterSettings);
    }


    // Directional Light�̓��e��Shader�ɔ��f�����܂�
    private void SetUpDirectionalLightParam(List<VisibleLight> visibleLights)
    {
        if( visibleLights.Count <= 0 ){
            return;
        }
        foreach( var visibleLight in visibleLights)
        {
            if (visibleLight.lightType == LightType.Directional)
            {
                Vector4 dir = -visibleLight.localToWorld.GetColumn(2) ;
                Shader.SetGlobalVector(ShaderNameHash.LightColor0, visibleLight.finalColor);
                Shader.SetGlobalVector(ShaderNameHash.WorldSpaceLightPos0, new Vector4(dir.x,dir.y,dir.z,0.0f) );
                break;
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        instance = null;
        if (cmd != null)
        {
            cmd.Dispose();
            cmd = null;
        }
    }
}
