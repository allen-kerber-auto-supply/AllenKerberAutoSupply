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
  @Output() addCustomerRequested = new EventEmitter<SalesCustomer>();
  @Output() deleteRepRequested = new EventEmitter<SalesRep>();
  @Output() assignCustomerRequested = new EventEmitter<void>();
  @Output() deleteCustomerRequested = new EventEmitter<SalesCustomer>();
  @Output() updateCustomerRequested = new EventEmitter<SalesCustomer>();
  @Output() filtersChanged = new EventEmitter<void>();

  editingCustomer: SalesCustomer = {};

  get editableCustomers(): SalesCustomer[] {
    return (this.allCustomers.length ? this.allCustomers : this.customers)
      .filter(customer => !!customer.customerNumber)
      .sort((a, b) => this.accountName(a).localeCompare(this.accountName(b)));
  }

  beginEditByNumber(customerNumber: number | SalesCustomer): void {
    this.editingCustomer = typeof customerNumber === 'object'
      ? { ...customerNumber }
      : this.editableCustomers.find(customer => customer.customerNumber === customerNumber) || {};
  }

  cancelEdit(): void {
    this.editingCustomer = {};
  }

  saveEdit(): void {
    const customer = {
      ...this.editingCustomer,
      customerName: (this.editingCustomer.customerName || '').trim(),
      contactName: (this.editingCustomer.contactName || '').trim(),
      contactPhone: (this.editingCustomer.contactPhone || '').trim()
    };
    if (!customer.customerName) return;
    if (customer.customerNumber) {
      this.updateCustomerRequested.emit(customer);
    } else {
      this.addCustomerRequested.emit(customer);
    }
    this.editingCustomer = {};
  }

}
