import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SalesCall, SalesCustomer, SalesRep } from '../shared/models';
import { SalesService } from './sales.service';

function today(): string { return new Date().toISOString().slice(0, 10); }
function padTime(value: number): string { return value.toString().padStart(2, '0'); }
function currentTimePart(getValue: (date: Date) => number): number { return getValue(new Date()); }
function currentTimeValue(): string { return `${padTime(currentTimePart(date => date.getHours()))}:${padTime(currentTimePart(date => date.getMinutes()))}`; }

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
  callTime = currentTimeValue();
  durationHours = 0;
  durationMinutes = 0;

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

  get hasInvalidCallTiming(): boolean {
    return !!this.validateCallTiming();
  }

  get callTimeError(): string {
    return this.validateCallTime();
  }

  get durationHoursError(): string {
    return this.validateDurationHours();
  }

  get durationMinutesError(): string {
    return this.validateDurationMinutes();
  }

  get durationTotalError(): string {
    return this.validateDurationTotal();
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
    this.newCall = { accountName: '', contactName: '', phone: '', comments: '', repEmail: '', repName: '', status: 1, callDate: today(), followUpDate: '' };
    this.callTime = currentTimeValue();
    this.durationHours = 0;
    this.durationMinutes = 0;
    this.setDefaultRep();
    this.selectedCustomerHistory = [];
    this.callErrorMessage = '';
  }

  saveNewCall(): void {
    if (!this.newCall.accountName.trim()) {
      this.callErrorMessage = 'Account name is required.';
      return;
    }

    const validationMessage = this.validateCallTiming();
    if (validationMessage) {
      this.callErrorMessage = validationMessage;
      return;
    }

    this.savingCall = true;
    this.callErrorMessage = '';
    const [callTimeHours, callTimeMinutes] = this.callTime.split(':').map(part => Number.parseInt(part, 10));
    const payload: SalesCall = {
      ...this.newCall,
      callDate: `${this.newCall.callDate}T${padTime(callTimeHours)}:${padTime(callTimeMinutes)}:00`,
      callDuration: this.durationHours * 60 + this.durationMinutes,
      followUpDate: this.newCall.followUpDate || undefined
    };
    const rep = this.reps.find(item => (item.repEmail || item.email || '').toLowerCase() === (payload.repEmail || '').toLowerCase());
    if (rep) payload.repName = rep.repName || rep.name || '';

    this.salesService.createCall(payload).subscribe({
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
    return { accountName: '', contactName: '', phone: '', comments: '', repEmail: '', repName: '', status: 1, callDate: today(), followUpDate: '' };
  }

  private validateCallTiming(): string {
    return this.callTimeError || this.durationHoursError || this.durationMinutesError || this.durationTotalError;
  }

  private isWholeNumberInRange(value: number, min: number, max: number): boolean {
    return Number.isInteger(value) && value >= min && value <= max;
  }

  private validateCallTime(): string {
    if (!this.callTime) {
      return 'Call time is required.';
    }

    if (!/^([01]\d|2[0-3]):([0-5]\d)$/.test(this.callTime)) {
      return 'Enter a valid call time.';
    }

    return '';
  }

  private validateDurationHours(): string {
    if (!this.isWholeNumberInRange(this.durationHours, 0, 8)) {
      return 'Hours must be a whole number between 0 and 8.';
    }

    return '';
  }

  private validateDurationMinutes(): string {
    if (!this.isWholeNumberInRange(this.durationMinutes, 0, 59)) {
      return 'Minutes must be a whole number between 0 and 59.';
    }

    return '';
  }

  private validateDurationTotal(): string {
    if (!this.durationHoursError && !this.durationMinutesError && this.durationHours === 8 && this.durationMinutes > 0) {
      return 'Call duration cannot exceed 8:00.';
    }

    return '';
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
}
