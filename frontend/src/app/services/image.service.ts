import { Injectable } from '@angular/core';

/**
 * 图片压缩服务：Canvas 缩放 + JPEG 压缩。
 * 在客户端压缩后再上传，节省带宽和存储。
 */
@Injectable({ providedIn: 'root' })
export class ImageService {

  /** 最大宽度 px，超出等比缩放 */
  private readonly MAX_WIDTH = 800;
  /** JPEG 质量 0-1 */
  private readonly QUALITY = 0.7;

  /**
   * 压缩图片，返回 Base64 字符串（带 data:image/jpeg;base64, 前缀）。
   * 如果图片本身已经很小，不做压缩直接返回。
   */
  compress(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        const img = new Image();
        img.onload = () => {
          const { width, height } = this.getScaledSize(img.width, img.height);

          // 如果原图已经很小，直接用原图
          if (width >= img.width && file.size < 200 * 1024) {
            resolve(reader.result as string);
            return;
          }

          const canvas = document.createElement('canvas');
          canvas.width = width;
          canvas.height = height;
          const ctx = canvas.getContext('2d')!;
          ctx.drawImage(img, 0, 0, width, height);

          resolve(canvas.toDataURL('image/jpeg', this.QUALITY));
        };
        img.onerror = () => reject(new Error('图片加载失败'));
        img.src = reader.result as string;
      };
      reader.onerror = () => reject(new Error('文件读取失败'));
      reader.readAsDataURL(file);
    });
  }

  private getScaledSize(origW: number, origH: number): { width: number; height: number } {
    if (origW <= this.MAX_WIDTH) return { width: origW, height: origH };
    const ratio = this.MAX_WIDTH / origW;
    return { width: this.MAX_WIDTH, height: Math.round(origH * ratio) };
  }
}
