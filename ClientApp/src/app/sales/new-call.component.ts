import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SalesCall, SalesCustomer, SalesRep } from '../shared/models';
import { SalesService } from './sales.service';

function today(): string { return new Date().toISOString().slice(0, 10); }
function nowTime(): string { return new Date().toISOString().slice(11, 16); }

@Component({
  selector: 'app-new-call',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './new-call.component.html',
  styleUrl: './new-call.component.css'
})
export class NewCallComponent implements OnChanges {
  @Input() reps: SalesRep[] = [];
  @Input() customers: SalesCustomer[] = [];
  @Input() accountNameOptions: string[] = [];
  @Input() isAdmin = false;
  @Input() currentUserEmail = '';
  @Output() saved = new EventEmitter<void>();

  newCall: SalesCall = this.emptyCall();
  selectedCustomerHistory: SalesCall[] = [];
  loadingCustomerHistory = false;
  savingCall = false;
  callErrorMessage = '';
  callTime = nowTime();
  callDurationHours: number | null = 0;
  callDurationMinutes: number | null = 0;

  constructor(private readonly salesService: SalesService) {
    this.resetNewCallForm();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['reps'] || changes['currentUserEmail'] || changes['isAdmin']) && !this.newCall.accountName) {
      this.setDefaultRep();
    }
  }

  get selectedCustomerHasAssignedRep(): boolean {
    return this.assignedRepEmails(this.findCustomer(this.newCall.accountName)).length > 0;
  }

  isAssignedSalesCustomer(accountName: string): boolean {
    return this.assignedRepEmails(this.findCustomer(accountName)).length > 0;
  }

  isAccountProspect(accountName: string): boolean {
    if (!accountName) return false;
    return !this.customers.some(customer => this.accountName(customer).toLowerCase() === accountName.trim().toLowerCase());
  }

  onAccountNameChange(): void {
    const name = (this.newCall.accountName || '').trim();
    if (!name) {
      this.selectedCustomerHistory = [];
      return;
    }

    const customer = this.findCustomer(name);
    this.newCall.isProspect = !customer;
    if (customer) this.applyAssignedRep(customer);

    this.loadingCustomerHistory = true;
    this.salesService.getCallsByAccount(name).subscribe({
      next: history => {
        this.selectedCustomerHistory = history || [];
        this.loadingCustomerHistory = false;
      },
      error: () => {
        this.selectedCustomerHistory = [];
        this.loadingCustomerHistory = false;
      }
    });
  }

  resetNewCallForm(): void {
    this.newCall = { accountName: '', contactName: '', phone: '', comments: '', callDuration: 0, repEmail: '', repName: '', status: 1, callDate: today(), followUpDate: '' };
    this.callTime = nowTime();
    this.callDurationHours = 0;
    this.callDurationMinutes = 0;
    this.setDefaultRep();
    this.selectedCustomerHistory = [];
    this.callErrorMessage = '';
  }

  saveNewCall(): void {
    if (!this.newCall.accountName.trim()) {
      this.callErrorMessage = 'Account name is required.';
      return;
    }

    const durationHours = this.parseWholeMinutes(this.callDurationHours);
    if (durationHours === null) {
      this.callErrorMessage = 'Duration hours must be a whole number of 0 or more.';
      return;
    }

    const durationMinutes = this.parseWholeMinutes(this.callDurationMinutes);
    if (durationMinutes === null || durationMinutes > 59) {
      this.callErrorMessage = 'Duration minutes must be a whole number between 0 and 59.';
      return;
    }

    const callDateTime = this.combineCallDateAndTime(this.newCall.callDate, this.callTime);
    if (!callDateTime) {
      this.callErrorMessage = 'A valid call date and time are required.';
      return;
    }

    this.savingCall = true;
    this.callErrorMessage = '';
    const rep = this.reps.find(item => (item.repEmail || item.email || '').toLowerCase() === (this.newCall.repEmail || '').toLowerCase());
    if (rep) this.newCall.repName = rep.repName || rep.name || '';

    this.salesService.createCall({
      ...this.newCall,
      callDate: callDateTime,
      callDuration: (durationHours * 60) + durationMinutes,
      followUpDate: this.newCall.followUpDate || undefined
    }).subscribe({
      next: () => {
        this.savingCall = false;
        this.resetNewCallForm();
        this.saved.emit();
      },
      error: error => {
        this.savingCall = false;
        this.callErrorMessage = error.error?.message || 'Failed to save call record.';
      }
    });
  }

  getRepDisplayName(repName?: string, repEmail?: string): string {
    if (repName) return repName;
    const rep = this.reps.find(item => (item.repEmail || item.email || '').toLowerCase() === (repEmail || '').toLowerCase());
    return rep?.repName || rep?.name || repEmail || 'Unassigned';
  }

  private emptyCall(): SalesCall {
    return { accountName: '', contactName: '', phone: '', comments: '', callDuration: 0, repEmail: '', repName: '', status: 1, callDate: today(), followUpDate: '' };
  }

  private setDefaultRep(): void {
    this.newCall.repEmail = !this.isAdmin && this.currentUserEmail
      ? this.currentUserEmail
      : (this.reps[0]?.repEmail || this.reps[0]?.email || this.currentUserEmail || '');
  }

  private accountName(customer: SalesCustomer): string {
    return customer.customerName || customer.accountName || '';
  }

  private findCustomer(accountName: string): SalesCustomer | undefined {
    const normalized = (accountName || '').trim().toLowerCase();
    return this.customers.find(customer => this.accountName(customer).trim().toLowerCase() === normalized);
  }

  private assignedRepEmails(customer: SalesCustomer | undefined): string[] {
    return customer?.assignedSalesReps || (customer?.repEmail ? [customer.repEmail] : []);
  }

  private applyAssignedRep(customer: SalesCustomer): void {
    const assigned = this.assignedRepEmails(customer)[0];
    if (assigned) this.newCall.repEmail = assigned;
  }

  private combineCallDateAndTime(callDate?: string, callTime?: string): string | null {
    const normalizedDate = (callDate || '').trim();
    const normalizedTime = (callTime || '').trim();
    return normalizedDate && /^\d{2}:\d{2}$/.test(normalizedTime)
      ? `${normalizedDate}T${normalizedTime}:00`
      : null;
  }

  private parseWholeMinutes(value: number | null): number | null {
    if (value === null || value === undefined) return 0;
    return Number.isInteger(value) && value >= 0 ? value : null;
  }
}
