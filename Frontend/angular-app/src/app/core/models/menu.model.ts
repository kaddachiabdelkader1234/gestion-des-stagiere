export interface MenuItem {
  label: string;
  href: string;
}

export interface FooterLink {
  section: string;
  links: MenuItem[];
}
