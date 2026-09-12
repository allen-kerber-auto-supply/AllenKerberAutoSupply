import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SalesRep } from '../shared/models';

export type SalesTab = 'scheduled' | 'new-call' | 'history' | 'admin';

@Component({
  selector: 'app-sales-navigation',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './sales-navigation.component.html',
  styleUrl: './sales-navigation.component.css'
})
export class SalesNavigationComponent {
  @Input() isAdmin = false;
  @Input() canViewAdminTab = false;
  @Input() reps: SalesRep[] = [];
  @Input() scheduledCount = 0;
  @Input() activeTab: SalesTab = 'scheduled';
  @Input() selectedRep = '';

  @Output() tabChange = new EventEmitter<SalesTab>();
  @Output() selectedRepChange = new EventEmitter<string>();
  @Output() repFilterChange = new EventEmitter<void>();

  selectTab(tab: SalesTab) {
    this.tabChange.emit(tab);
  }

  updateRep(value: string) {
    this.selectedRepChange.emit(value);
    this.repFilterChange.emit();
  }
}
