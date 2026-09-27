import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ScanNfceComponent } from './scan-nfce.component';

describe('ScanNfceComponent', () => {
  let component: ScanNfceComponent;
  let fixture: ComponentFixture<ScanNfceComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScanNfceComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ScanNfceComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
