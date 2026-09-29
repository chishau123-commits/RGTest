# AutoDL 方案：自己租 GPU 跑 ComfyUI

Comfy Cloud 的 API 需要平台 Pro 档，而 platform 上的工作区是 FREE，`/api/prompt` 一律返回
`FREE_TIER_NOT_ALLOWED`。AutoDL 绕开整个问题：租一台带 GPU 的机器，自己装 ComfyUI，
**没有档位限制，也没有模型白名单**，想装什么模型就装什么。

## 为什么这条路的代码是现成的

本地 ComfyUI 的 REST 路由就是最标准的那套（裸 `/prompt`、`/history/<id>`、`/object_info`），
而 `BGA/tools/comfy_cloud.py` 已经同时支持两套路由：

| 地址 | 路由前缀 | 鉴权 |
|---|---|---|
| `https://cloud.comfy.org` | `/api` | 需要 API key |
| `http://127.0.0.1:6006`（SSH 隧道）| 无 | 不需要 |

所以只要把 `COMFY_URL` 指向隧道端口，**现有的生成脚本一行都不用改**。

## 步骤

### 1. 租实例

在 [autodl.com](https://www.autodl.com) 租一台。选卡建议：

| 显存 | 能跑 | 说明 |
|---|---|---|
| **24GB（RTX 4090 / 3090）** | Wan 2.2 14B fp8 @720p、5B、FLF2V | 够用，性价比最高 |
| **40GB+（A100 40G / 6000 Ada）** | 上面全部 + 更长帧数裕度 | 要 1080p 或长片段再上 |

镜像选 **PyTorch 2.x + CUDA 12.x**（不要选带 ComfyUI 的第三方镜像，版本常常过旧）。

⚠️ 数据盘要够大：**Wan 2.2 全套模型约 40–50GB**。默认系统盘通常不够，
租的时候把数据盘设到 100GB 以上（数据盘按量计费，比系统盘便宜）。

### 2. 进实例里的终端

在控制台点「JupyterLab」，开一个 Terminal。

### 3. 装环境

把仓库里的 `BGA/tools/autodl_setup.sh` 上传到实例（JupyterLab 可以直接拖文件），然后：

```bash
bash autodl_setup.sh 2>&1 | tee ~/setup.log
```

脚本会做四件事：

1. 克隆 ComfyUI 到 `/root/autodl-tmp/ComfyUI`（数据盘，不占系统盘）
2. 下载 Wan 2.2 全套模型：`wan2.2_ti2v_5B`、`wan2.2_i2v_14B`（高/低噪声两阶段）、
   `wan2.1_flf2v_720p_14B`（**连接片段用的首尾帧模型**）、`umt5_xxl`、两个 VAE、
   以及 4 步加速 LoRA
3. 装 ComfyUI-Manager（缺什么节点可以在界面里点装）
4. 生成启动脚本

下载走 `hf-mirror.com`（国内可达），不依赖 huggingface.co。

### 4. 启动

```bash
bash /root/autodl-tmp/start_comfy.sh
```

界面访问有两种方式：

- **自定义服务**：AutoDL 把实例的 **6006 端口**映射到公网，在控制台「自定义服务」里拿到地址
- **SSH 隧道**（需要本地端口时用这个）：
  ```
  ssh -CNg -L 6006:127.0.0.1:6006 root@<实例host> -p <实例端口>
  ```
  然后本地浏览器开 `http://127.0.0.1:6006`

### 5. 让我接手

做完第 4 步，告诉我你用的是哪种方式：

- **SSH 隧道** → 我这边 `COMFY_URL=http://127.0.0.1:6006`，直接开始跑
- **自定义服务公网地址** → 把地址给我（形如 `https://u39-xxxx.nmb2.seetacloud.com:8443`），
  我设成 `COMFY_URL`。注意这个地址是**公网可达**的，等于把 ComfyUI 暴露在互联网上，
  跑完建议换端口或停实例

然后我会：

1. 用 `comfy_probe_capabilities.py` 确认模型全部就位
2. 提交 **1 个**最小任务验通
3. 跑 5 秒样片，**测出实际单次耗时**
4. 用同一张参考图跑 Wan 14B / HunyuanVideo 1.5 对比，测 1080p 是否劣化

## 成本量级

AutoDL 按小时计费（4090 大约 ¥2/小时上下，具体以控制台为准）。生成是 GPU 独占的，
所以**跑完就关机**最省钱。按 20 秒全流程 11 次生成估算，真正占卡的时间大概几小时，
加上装环境的 1 小时，**总成本远低于包月**。

省钱的点：
- 模型下载只做一次，之后实例「关机」不释放数据盘，下次开机不用重下
- 用 4 步 Lightning LoRA，14B 采样从 ~20 步降到 4 步
- 先用 5B 或蒸馏版试构图，定稿再用 14B

## 待办：确认模型下载源

`autodl_setup.sh` 里的 HuggingFace 仓库路径是按 `Comfy-Org` 的官方 repackage 命名写的，
但**仓库内文件路径需要第一次运行时核对**。如果某个 `fetch` 报 FAIL，把那个文件名发我，
我改路径。也可以改用 ModelScope 镜像（国内更快）。

## 与之前路线的对比

| | Comfy Cloud API | AutoDL |
|---|---|---|
| 前置条件 | Pro 订阅（platform 工作区要 Pro）| 充值即可 |
| 模型限制 | 只能用平台提供的 | **任意模型** |
| 计费 | 积分 | 按小时 |
| 1080p | HunyuanVideo 1.5 可用，但没末帧 | **全套都能装** |
| 网络 | 云端，无需隧道 | 需要隧道或自定义服务 |
| 环境搭建 | 零 | 约 1 小时（一次性）|

AutoDL 唯一的代价是**首次搭环境**，换来的是**没有任何模型和档位限制** ——
对"要 1080p、要高帧率、要无缝接缝"这三个要求同时成立，这条路是最有把握的。
