import { CommonModule } from '@angular/common';
import { Component, OnDestroy } from '@angular/core';
import { Html5Qrcode, Html5QrcodeSupportedFormats } from 'html5-qrcode';
import { ImportNfceOutcome, Purchase, PurchaseService } from '../../core/purchase.service';

type ScanState = 'idle' | 'scanning' | 'sending' | 'success' | 'duplicate' | 'error';

const SCANNER_ELEMENT_ID = 'nfce-reader';

@Component({
  selector: 'app-scan-nfce',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './scan-nfce.component.html',
  styleUrl: './scan-nfce.component.scss',
})
export class ScanNfceComponent implements OnDestroy {
  readonly readerElementId = SCANNER_ELEMENT_ID;

  state: ScanState = 'idle';
  errorMessage = '';
  lastPurchase: Purchase | null = null;

  private scanner: Html5Qrcode | null = null;

  constructor(private readonly purchaseService: PurchaseService) {}

  async startScan(): Promise<void> {
    this.state = 'scanning';
    this.errorMessage = '';

    this.scanner = new Html5Qrcode(this.readerElementId, {
      formatsToSupport: [Html5QrcodeSupportedFormats.QR_CODE],
      verbose: false,
    });

    try {
      await this.scanner.start(
        { facingMode: 'environment' },
        { fps: 10, qrbox: { width: 250, height: 250 } },
        (decodedText) => this.onDecoded(decodedText),
        () => {
          // ignora leituras sem sucesso; a camera continua tentando
        }
      );
    } catch {
      this.state = 'error';
      this.errorMessage = 'Nao foi possivel acessar a camera. Verifique as permissoes do navegador.';
    }
  }

  async ngOnDestroy(): Promise<void> {
    await this.stopScanner();
  }

  private async onDecoded(nfceUrl: string): Promise<void> {
    await this.stopScanner();
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

  private async stopScanner(): Promise<void> {
    if (!this.scanner) {
      return;
    }

    try {
      await this.scanner.stop();
      this.scanner.clear();
    } catch {
      // camera ja parada
    } finally {
      this.scanner = null;
    }
  }
}
