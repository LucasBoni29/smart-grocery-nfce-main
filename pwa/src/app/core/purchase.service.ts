import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PurchaseItem {
  id: number;
  quantity: number;
  unit: string | null;
  unitPrice: number;
  totalPrice: number;
  product: {
    id: number;
    name: string;
    brand: string | null;
    unit: string | null;
    barcode: string | null;
  };
}

export interface Purchase {
  id: number;
  storeName: string | null;
  nfceUrl: string | null;
  nfceAccessKey: string | null;
  purchasedAt: string;
  totalAmount: number;
  items: PurchaseItem[];
}

export type ImportNfceOutcome =
  | { status: 'created' | 'completed'; purchase: Purchase }
  | { status: 'duplicate' }
  | { status: 'invalid' | 'unprocessable'; message: string };

@Injectable({ providedIn: 'root' })
export class PurchaseService {
  private readonly baseUrl = `${environment.apiBaseUrl}/purchases`;

  constructor(private readonly http: HttpClient) {}

  importNfce(nfceUrl: string): Observable<ImportNfceOutcome> {
    return new Observable<ImportNfceOutcome>((subscriber) => {
      this.http
        .post<Purchase>(`${this.baseUrl}/import-nfce`, { nfceUrl }, { observe: 'response' })
        .pipe(
          catchError((error: HttpErrorResponse) => {
            if (error.status === 409) {
              subscriber.next({ status: 'duplicate' });
              subscriber.complete();
              return throwError(() => error);
            }

            if (error.status === 422) {
              subscriber.next({
                status: 'unprocessable',
                message: error.error?.message ?? 'A NFC-e nao pode ser processada.',
              });
              subscriber.complete();
              return throwError(() => error);
            }

            if (error.status === 400) {
              subscriber.next({
                status: 'invalid',
                message: 'A URL escaneada nao contem uma chave de NFC-e valida.',
              });
              subscriber.complete();
              return throwError(() => error);
            }

            subscriber.next({
              status: 'unprocessable',
              message: 'Nao foi possivel falar com a API. Verifique a conexao.',
            });
            subscriber.complete();
            return throwError(() => error);
          })
        )
        .subscribe({
          next: (response) => {
            subscriber.next({
              status: response.status === 201 ? 'created' : 'completed',
              purchase: response.body as Purchase,
            });
            subscriber.complete();
          },
          error: () => {
            // outcome ja emitido pelo catchError acima
          },
        });
    });
  }
}
