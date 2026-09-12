import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface InvoiceImageLookup {
  storeNumber?: number;
  totalPages?: number;
  pages?: Array<{ pageIndex: number }>;
}

@Injectable({ providedIn: 'root' })
export class InvoiceImageService {
  private readonly http = inject(HttpClient);

  lookup(invoiceNumber: string, storeNumber: number): Observable<InvoiceImageLookup> {
    const invoice = encodeURIComponent(invoiceNumber);
    const url = storeNumber > 0
      ? `/api/invoice-images/${storeNumber}/${invoice}/lookup`
      : `/api/invoice-images/${invoice}/lookup`;
    return this.http.get<InvoiceImageLookup>(url);
  }

  loadPage(url: string): Observable<Blob> {
    return this.http.get(url, { responseType: 'blob' });
  }

  pageUrl(invoiceNumber: string, storeNumber: number, pageIndex: number, cacheBust?: number): string {
    const invoice = encodeURIComponent(invoiceNumber);
    const suffix = `page=${pageIndex}${cacheBust ? `&t=${cacheBust}` : ''}`;
    return storeNumber > 0
      ? `/api/invoice-images/${storeNumber}/${invoice}?${suffix}`
      : `/api/invoice-images/${invoice}?${suffix}`;
  }
}