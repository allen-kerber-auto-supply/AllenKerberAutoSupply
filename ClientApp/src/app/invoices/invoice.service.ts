import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Invoice } from '../shared/models';

@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private readonly http = inject(HttpClient);

  getByNumber(invoiceNumber: string): Observable<Invoice[]> {
    return this.http.get<Invoice[]>(`/api/invoices?invoiceNumber=${encodeURIComponent(invoiceNumber)}`);
  }

  searchByNumber(invoiceNumber: string): Observable<Invoice[]> {
    return this.http.get<Invoice[]>('/api/invoices', { params: { invoiceNumber } });
  }

  searchByDate(beginDate: string, endDate: string, customerNumber?: number): Observable<Invoice[]> {
    const params: Record<string, string> = { beginDate, endDate };
    if (customerNumber != null) params['customerNumber'] = String(customerNumber);
    return this.http.get<Invoice[]>('/api/invoices/by-date', { params });
  }
}