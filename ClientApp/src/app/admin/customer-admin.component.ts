import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { FirestoreCustomer } from '../shared/models';

@Component({
  selector: 'app-customer-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './customer-admin.component.html',
  styleUrl: './customer-admin.component.css'
})
export class CustomerAdminComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly pageSize = 30;
  customers: FirestoreCustomer[] = [];
  filterText = '';
  visibleCount = this.pageSize;
  selectedCustomer: FirestoreCustomer | null = null;
  form: FirestoreCustomer = this.emptyCustomer();
  emailsText = '';
  loading = false;
  saving = false;
  message = '';
  error = '';

  get filteredCustomers(): FirestoreCustomer[] {
    const filter = this.filterText.trim().toLowerCase();
    const matchingCustomers = filter
      ? this.customers.filter(customer => [customer.customerName, customer.customerNumber, customer.vendorId, customer.city, customer.state, customer.zip]
        .some(value => String(value ?? '').toLowerCase().includes(filter)))
      : [...this.customers];

    return matchingCustomers;
  }

  get visibleCustomers(): FirestoreCustomer[] {
    return this.filteredCustomers.slice(0, this.visibleCount);
  }

  ngOnInit(): void { this.loadCustomers(); }

  loadCustomers(): void {
    this.loading = true;
    this.error = '';
    this.http.get<FirestoreCustomer[]>('/api/customers/admin').subscribe({
      next: customers => { this.customers = customers || []; this.resetVisibleCustomers(); this.loading = false; },
      error: response => { this.loading = false; this.error = this.getErrorMessage(response, 'Unable to load customers.'); }
    });
  }

  updateFilter(value: string): void {
    this.filterText = value;
    this.resetVisibleCustomers();
  }

  loadMoreCustomers(event: Event): void {
    const element = event.target as HTMLElement;
    if (element.scrollTop + element.clientHeight >= element.scrollHeight - 80 && this.visibleCount < this.filteredCustomers.length) {
      this.visibleCount += this.pageSize;
    }
  }

  trackCustomer(_: number, customer: FirestoreCustomer): number {
    return customer.customerNumber;
  }

  private resetVisibleCustomers(): void {
    this.visibleCount = this.pageSize;
  }

  startEdit(customer: FirestoreCustomer): void {
    this.selectedCustomer = customer;
    this.form = { ...customer, emails: [...(customer.emails || [])] };
    this.emailsText = (customer.emails || []).join('\n');
    this.message = '';
    this.error = '';
  }

  cancelEdit(): void {
    this.selectedCustomer = null;
    this.message = '';
    this.error = '';
  }

  save(): void {
    this.message = '';
    this.error = '';
    this.form.emails = this.emailsText.split(/[,\n]/).map(email => email.trim()).filter(Boolean);
    if (!this.form.customerNumber || !this.form.customerName.trim()) {
      this.error = 'Customer number and customer name are required.';
      return;
    }

    this.saving = true;
    if (!this.selectedCustomer) return;

    const request = this.http.put<FirestoreCustomer>(`/api/customers/${this.selectedCustomer.customerNumber}`, this.form);
    request.subscribe({
      next: () => {
        this.saving = false;
        this.message = 'Customer updated.';
        this.loadCustomers();
      },
      error: response => { this.saving = false; this.error = this.getErrorMessage(response, 'Unable to save customer.'); }
    });
  }

  private getErrorMessage(response: { status?: number; error?: unknown; message?: string }, fallback: string): string {
    const error = response.error;
    const detail = typeof error === 'string' ? error : (error as { detail?: string; message?: string } | null)?.detail || (error as { message?: string } | null)?.message;
    const responseMessage = response.message && !response.message.includes('Http failure during parsing') ? response.message : '';
    return detail ? `${detail}${response.status ? ` (${response.status})` : ''}` : responseMessage || `${fallback}${response.status ? ` (${response.status})` : ''}`;
  }

  private emptyCustomer(): FirestoreCustomer {
    return { customerNumber: 0, customerName: '', showPo: false, vendorId: '', statementOrInvoice: 'I', address1: '', address2: '', city: '', state: '', zip: '', emails: [] };
  }
}
