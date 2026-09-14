import { CommonModule } from '@angular/common';
import { Component, ElementRef, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { Destination, Theme } from '../shared/models';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './app-header.component.html',
  styleUrl: './app-header.component.css'
})
export class AppHeaderComponent {
  @Input() theme: Theme = 'light';
  @Input() authenticated = false;
  @Input() canManageUsers = false;
  @Input() canManageCustomers = false;
  @Input() hasDualRoles = false;
  @Input() destination: Destination = null;
  @Input() name = '';
  @Input() initials = '';
  @Input() signingOut = false;

  @Output() themeToggle = new EventEmitter<void>();
  @Output() adminRequested = new EventEmitter<void>();
  @Output() customerAdminRequested = new EventEmitter<void>();
  @Output() switchRequested = new EventEmitter<void>();
  @Output() logoutRequested = new EventEmitter<void>();

  menuOpen = false;

  constructor(private readonly elementRef: ElementRef) {}

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.menuOpen && !this.elementRef.nativeElement.contains(event.target)) this.menuOpen = false;
  }
}
