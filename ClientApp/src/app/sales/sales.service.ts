import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AccountSummary, PagedSalesCalls, SalesCall, SalesCustomer } from '../shared/models';

@Injectable({ providedIn: 'root' })
export class SalesService {
  private readonly http = inject(HttpClient);

  getReps() {
    return this.http.get<any[]>('/api/sales/reps');
  }

  getCustomers() {
    return this.http.get<any[]>('/api/sales/customers');
  }

  getUnassignedCustomers() {
    return this.http.get<any[]>('/api/sales/customers/unassigned');
  }

  getUpcomingCalls(params: Record<string, string> = {}) {
    return this.http.get<SalesCall[]>('/api/sales/calls/upcoming', { params });
  }

  getCallsByAccount(accountName: string) {
    return this.http.get<SalesCall[]>(`/api/sales/calls/by-account?accountName=${encodeURIComponent(accountName)}`);
  }

  createCall(call: SalesCall) {
    return this.http.post<SalesCall>('/api/sales/calls', call);
  }

  updateCall(callId: number, call: SalesCall) {
    return this.http.put(`/api/sales/calls/${callId}`, call);
  }

  deleteCall(callId: number) {
    return this.http.delete(`/api/sales/calls/${callId}`);
  }

  convertProspect(callId: number) {
    return this.http.post(`/api/sales/calls/${callId}/convert-prospect`, {});
  }

  getCalls(params: Record<string, string> = {}) {
    return this.http.get<PagedSalesCalls>('/api/sales/calls', { params });
  }

  getAccountSummaries(params: Record<string, string> = {}) {
    return this.http.get<AccountSummary[]>('/api/sales/calls/summary-by-account', { params });
  }

  addRep(repName: string, repEmail: string) {
    return this.http.post('/api/sales/reps', { repName, repEmail });
  }

  deleteRep(email: string) {
    return this.http.delete(`/api/sales/reps/${encodeURIComponent(email)}`);
  }

  assignCustomer(customerName: string, repEmail: string) {
    return this.http.post('/api/sales/assignments', { customerName, repEmail });
  }

  updateCustomer(customer: Pick<SalesCustomer, 'customerNumber' | 'customerName' | 'contactName' | 'contactPhone'>) {
    return this.http.put(`/api/sales/customers/${customer.customerNumber}`, {
      customerName: customer.customerName,
      contactName: customer.contactName || '',
      contactPhone: customer.contactPhone || ''
    });
  }

  mergeCustomers(survivingCustomerNumber: number, duplicateCustomerNumber: number) {
    return this.http.post('/api/sales/customers/merge', { survivingCustomerNumber, duplicateCustomerNumber });
  }

  unassignCustomer(customerName: string, repEmail: string) {
    return this.http.delete('/api/sales/assignments', { body: { customerName, repEmail } });
  }
}
