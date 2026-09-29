# 20 秒 BGA 生成：云端 ComfyUI 接法与当前阻塞

## 当前状态：账号被判为免费档

已用**两个不同的 API key** 测试，都返回同一错误：

```
HTTP 403 on /api/prompt
{"error":{"message":"API key authentication is not available for free tier accounts",
          "type":"FREE_TIER_NOT_ALLOWED"}}
```

同时：

- `/api/object_info`、`/api/upload/image` 用同一个 key **成功**（说明 key 本身有效、代码正确）
- 账号 `/api/user` 返回 `{"id":"EEUfmS2Vl5ci5wzHgvn91TwJdWF2","status":"active"}`

**结论：拒绝发生在账号层面，不是 key 的问题，也不是代码的问题。** 换 key 无效，因为限制绑在账号上。

需要在 [platform.comfy.org](https://platform.comfy.org) 确认该账号的订阅状态；若已付费仍如此，就是 platform 账号与付费账号不是同一个。

## 免费档也能走：浏览器手动跑

免费档**可以**在浏览器界面里跑工作流，只是不能用 API。工作流已导成可直接导入的画布文件：

`BGA/whitebox20/workflow-keyframe-hires.gui.json`（11 节点 / 13 连线）

导入后只需要：

1. **`LoadImage` 节点** → 上传 `BGA/whitebox45/keyframes/01_dew_garden.png`
2. 点 **Run**
3. 出图后下载，放到 `BGA/whitebox20/keyframes-hires/`

其余参数（模型、分辨率 1920×1080、提示词、采样步数 20 / cfg 2.5 / denoise 0.45）都已在画布文件里。

重新导出画布文件的命令：

```powershell
python BGA/tools/export_gui_workflow.py --api BGA/whitebox20/workflow-keyframe-hires.api.json --out BGA/whitebox20/workflow-keyframe-hires.gui.json
```

## 云端已确认可用的资源

| 用途 | 模型 / 节点 | 状态 |
|---|---|---|
| 连接片段（首尾帧） | `WanFirstLastFrameToVideo`、`FL_WanFirstLastFrameToVideo`、`WanFunInpaintToVideo`、`WanVideoVACEStartToEndFrame` | 在 |
| 连接片段专用模型 | `wan2.1_flf2v_720p_14B_fp16.safetensors` | 在 |
| 原片生成（你原来用的） | `wan2.2_ti2v_5B_fp16.safetensors` | 在 |
| 高画质原片生成 | `wan2.2_i2v_high_noise_14B_*` + `low_noise_14B_*` | 在（两阶段采样）|
| **1080p 原片生成** | `hunyuanvideo1.5_720p_i2v_*` + **`hunyuanvideo1.5_1080p_sr_distilled_fp16`** | 在 |
| 4 步加速 LoRA | `Wan2.2-Lightning_I2V-A14B-4steps-lora_HIGH/LOW_fp16` | 在 |
| 文本编码器 | `umt5_xxl_fp8_e4m3fn_scaled.safetensors` | 在 |
| VAE | `wan2.2_vae` / `wan_2.1_vae` / `qwen_image_vae` | 在 |
| 图像编辑（提清晰度） | `qwen_image_edit_2511_fp8mixed` + `qwen_2.5_vl_7b_fp8_scaled` + `qwen_image_vae` | 在 |
| 高帧率插值 | `minterpolate`（本地 ffmpeg，**不耗额度**）| 已验证可用 |

节点类总数 3761，Comfy Cloud v0.272.0。

## 两个硬约束（实测得出，不是推测）

### 1. 没有"更高刷新率的模型"

扫描了全部 3761 个节点类：HunyuanVideo 1.5 与 LTX-2 的生成节点**都没有 fps 参数**，只有帧数（`length` / `num_frames`）。帧率由模型架构决定。

要做 60fps 只有两条路：生成更多帧（时间与额度线性增加），或本地 `minterpolate` 插值（免费）。推荐后者。

### 2. HunyuanVideo 做不了连接片段

扫描全部节点，能同时接受首帧与末帧的只有：

| 节点 | 家族 |
|---|---|
| `WanFirstLastFrameToVideo` / `FL_WanFirstLastFrameToVideo` | Wan |
| `WanFunInpaintToVideo` / `WanVideoVACEStartToEndFrame` / `WanVideoImageToVideoEncode` | Wan |
| `CogVideoImageEncode` / `CosmosImageToVideoLatent` | CogVideo / Cosmos |
| `LtxApi25ImageToVideo` | LTX（云端 API 节点，另收费）|
| — | **HunyuanVideo 一个都没有** |

`HunyuanVideo15ImageToVideo` 只有 `start_image`。所以**无缝连接片段只能用 Wan**，而**真正的 1080p 只有 HunyuanVideo 1.5 有**。这是真实冲突，因此有三条路线：

| 路线 | 原片 | 连接片段 | 接缝 | 清晰度 |
|---|---|---|---|---|
| A | Wan 2.2 I2V 14B @720p | Wan FLF2V | 真无缝 | 720p 有效细节 |
| B | HunyuanVideo 1.5 @1920×1080 | 只能用叠化 | 有跳变 | 1080p（待验证超训练分辨率是否劣化）|
| C | HunyuanVideo 1.5 @1080p | Wan FLF2V | 接缝处画风会变 | 1080p |

路线 C 的风险：Kubeez 文档明确警告不要在同一条链里换模型，每个模型的颗粒与运动质感不同，混用会在接缝处产生"画风跳变"。

## 为什么必须先提参考图

```
参考图 1672×941  ──>  模型生成        ──>  交付
    ↑                    ↑                   ↑
 约 1600×900          Wan ≤1280×720       放大增加像素
 本身就不到 1080p      之上属于超范围      但不增加细节
```

最短板决定最终清晰度，而当前最短板是参考图。所以顺序是：先提参考图 → 再测模型原生 1080p 是否劣化 → 再定路线。

## 规模预估

20 秒 BGA 约需：4 张高分辨率参考图、4 段原片、3 个连接片段，合计 **11 次生成**（不含失败重试）。走 API 是一次性配好就能跑完的量；走浏览器手动则要点 11 次并手动下载。

