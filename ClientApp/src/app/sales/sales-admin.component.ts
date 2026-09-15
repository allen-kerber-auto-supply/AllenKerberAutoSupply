import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SalesCustomer, SalesRep } from '../shared/models';

@Component({
  selector: 'app-sales-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './sales-admin.component.html',
  styleUrl: './sales-admin.component.css'
})
export class SalesAdminComponent {
  @Input() reps: SalesRep[] = [];
  @Input() unassignedCustomers: SalesCustomer[] = [];
  @Input() customers: SalesCustomer[] = [];
  @Input() allCustomers: SalesCustomer[] = [];
  @Input() accountFilter = '';
  @Input() repFilter = '';
  @Input() assignmentMessage = '';
  @Input() newRep: SalesRep = {};
  @Input() newCustomerName = '';
  @Input() selectedRepEmail = '';
  @Input() accountName: (customer: SalesCustomer) => string = customer => customer.customerName || customer.accountName || '';
  @Input() assignedReps: (customer: SalesCustomer) => string = () => 'Unassigned';
  @Input() hasAssignments: (customer: SalesCustomer) => boolean = () => false;

  @Output() newRepChange = new EventEmitter<SalesRep>();
  @Output() newCustomerNameChange = new EventEmitter<string>();
  @Output() selectedRepEmailChange = new EventEmitter<string>();
  @Output() accountFilterChange = new EventEmitter<string>();
  @Output() repFilterChange = new EventEmitter<string>();
  @Output() addRepRequested = new EventEmitter<void>();
  @Output() deleteRepRequested = new EventEmitter<SalesRep>();
  @Output() assignCustomerRequested = new EventEmitter<void>();
  @Output() deleteCustomerRequested = new EventEmitter<SalesCustomer>();
  @Output() updateCustomerRequested = new EventEmitter<SalesCustomer>();
  @Output() mergeCustomersRequested = new EventEmitter<{ survivingCustomerNumber: number; duplicateCustomerNumber: number }>();
  @Output() filtersChanged = new EventEmitter<void>();

  editingCustomer: SalesCustomer | null = null;
  mergeSurvivorNumber = 0;
  mergeDuplicateNumber = 0;

  get editableCustomers(): SalesCustomer[] {
    return (this.allCustomers.length ? this.allCustomers : this.customers)
      .filter(customer => !!customer.customerNumber)
      .sort((a, b) => this.accountName(a).localeCompare(this.accountName(b)));
  }

  beginEdit(customer: SalesCustomer): void {
    this.editingCustomer = { ...customer };
  }

  cancelEdit(): void {
    this.editingCustomer = null;
  }

  saveEdit(): void {
    if (this.editingCustomer?.customerNumber && this.editingCustomer.customerName?.trim()) {
      this.updateCustomerRequested.emit({
        ...this.editingCustomer,
        customerName: this.editingCustomer.customerName.trim(),
        contactName: (this.editingCustomer.contactName || '').trim(),
        contactPhone: (this.editingCustomer.contactPhone || '').trim()
      });
      this.editingCustomer = null;
    }
  }

  mergeCustomers(): void {
    if (this.mergeSurvivorNumber && this.mergeDuplicateNumber && this.mergeSurvivorNumber !== this.mergeDuplicateNumber) {
      this.mergeCustomersRequested.emit({
        survivingCustomerNumber: this.mergeSurvivorNumber,
        duplicateCustomerNumber: this.mergeDuplicateNumber
      });
    }
  }
}
