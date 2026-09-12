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
  @Output() filtersChanged = new EventEmitter<void>();
}
