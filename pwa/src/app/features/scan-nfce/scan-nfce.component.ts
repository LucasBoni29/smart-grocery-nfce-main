import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, ViewChild } from '@angular/core';
import { readBarcodes } from 'zxing-wasm/reader';
import { ImportNfceOutcome, Purchase, PurchaseService } from '../../core/purchase.service';

type ScanState = 'idle' | 'scanning' | 'sending' | 'success' | 'duplicate' | 'error';

// Tempo entre tentativas de decodificacao. Nao precisa ser por frame: a nota
// nao sai andando, e decodificar com WASM tem custo. 300ms ja fica fluido.
const DECODE_INTERVAL_MS = 300;

@Component({
  selector: 'app-scan-nfce',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './scan-nfce.component.html',
  styleUrl: './scan-nfce.component.scss',
})
export class ScanNfceComponent implements OnDestroy {
  @ViewChild('video') videoRef?: ElementRef<HTMLVideoElement>;

  state: ScanState = 'idle';
  errorMessage = '';
  lastPurchase: Purchase | null = null;

  private stream: MediaStream | null = null;
  private canvas = document.createElement('canvas');
  private decodeTimer: ReturnType<typeof setInterval> | null = null;
  private decoding = false;
  private decoded = false;

  constructor(private readonly purchaseService: PurchaseService) {}

  async startScan(): Promise<void> {
    this.state = 'scanning';
    this.errorMessage = '';
    this.decoded = false;

    // Espera o Angular tirar a classe "hidden" do <video> antes de pedir a
    // camera - sem isso o elemento pode nao estar visivel/dimensionado ainda
    // quando o stream for anexado.
    await new Promise((resolve) => requestAnimationFrame(resolve));

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment' },
        audio: false,
      });
    } catch {
      this.state = 'error';
      this.errorMessage = 'Nao foi possivel acessar a camera. Verifique as permissoes do navegador.';
      return;
    }

    const video = this.videoRef?.nativeElement;
    if (!video) {
      this.state = 'error';
      this.errorMessage = 'Nao foi possivel iniciar a camera.';
      return;
    }

    video.srcObject = this.stream;
    await video.play();

    this.decodeTimer = setInterval(() => this.tryDecodeFrame(), DECODE_INTERVAL_MS);
  }

  async ngOnDestroy(): Promise<void> {
    this.stopScanner();
  }

  private async tryDecodeFrame(): Promise<void> {
    if (this.decoding || this.decoded) {
      return;
    }

    const video = this.videoRef?.nativeElement;
    if (!video || video.readyState < video.HAVE_CURRENT_DATA || video.videoWidth === 0) {
      return;
    }

    this.decoding = true;
    try {
      this.canvas.width = video.videoWidth;
      this.canvas.height = video.videoHeight;
      const ctx = this.canvas.getContext('2d');
      if (!ctx) {
        return;
      }
      ctx.drawImage(video, 0, 0, this.canvas.width, this.canvas.height);
      const imageData = ctx.getImageData(0, 0, this.canvas.width, this.canvas.height);

      const results = await readBarcodes(imageData, {
        formats: ['QRCode'],
        tryHarder: true,
      });

      if (results.length > 0 && !this.decoded) {
        this.decoded = true;
        await this.onDecoded(results[0].text);
      }
    } catch {
      // frame ruim (desfocado, sem QR) - so tenta de novo no proximo timer
    } finally {
      this.decoding = false;
    }
  }

  private async onDecoded(nfceUrl: string): Promise<void> {
    this.stopScanner();
    this.state = 'sending';

    this.purchaseService.importNfce(nfceUrl).subscribe((outcome: ImportNfceOutcome) => {
      switch (outcome.status) {
        case 'created':
        case 'completed':
          this.state = 'success';
          this.lastPurchase = outcome.purchase;
          break;
        case 'duplicate':
          this.state = 'duplicate';
          break;
        case 'invalid':
        case 'unprocessable':
          this.state = 'error';
          this.errorMessage = outcome.message;
          break;
      }
    });
  }

  private stopScanner(): void {
    if (this.decodeTimer !== null) {
      clearInterval(this.decodeTimer);
      this.decodeTimer = null;
    }

    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = null;

    const video = this.videoRef?.nativeElement;
    if (video) {
      video.srcObject = null;
    }
  }
}
