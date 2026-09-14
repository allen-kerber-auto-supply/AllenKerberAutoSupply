import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Invoice } from '../shared/models';

export interface InvoiceSearchPage {
  items: Invoice[];
  hasMore: boolean;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private readonly http = inject(HttpClient);

  getByNumber(invoiceNumber: string): Observable<InvoiceSearchPage> {
    return this.http.get<InvoiceSearchPage>(`/api/invoices?invoiceNumber=${encodeURIComponent(invoiceNumber)}&page=0`);
  }

  searchByNumber(invoiceNumber: string, sortKey: string, sortDirection: string, page: number): Observable<InvoiceSearchPage> {
    return this.http.get<InvoiceSearchPage>('/api/invoices', { params: { invoiceNumber, sortKey, sortDirection, page } });
  }

  searchByDate(beginDate: string, endDate: string, customerNumber: number | undefined, sortKey: string, sortDirection: string, page: number): Observable<InvoiceSearchPage> {
    const params: Record<string, string | number> = { beginDate, endDate, sortKey, sortDirection, page };
    if (customerNumber != null) params['customerNumber'] = String(customerNumber);
    return this.http.get<InvoiceSearchPage>('/api/invoices/by-date', { params });
  }
}