This repo contains trade robots for sending transactions to Moscow stock exchange through SmartCom broker API provided by D8 Capital (previously known as IT-Invest)
(website: https://d8.capital/)

### trader1 (initial version)
  - gets derivative info (such as bars, quotes, ticks) and issues buy/sell transactions accordingly
  - consumes data records from QUIK trader app (https://arqatech.com/en/products/quik/) streamed into MS Access, aggregates them and displays on console 

### trader2 (upgraded version of trader1)
  - compares buy-sell volume differences across ticks and decides the type of next transaction (buy vs. sell)  
  - applies windowing, computes average & standard deviation of accumulated data window and decides the type of next transaction (buy vs. sell)
  - checks time difference between client machine and stock exchange, and adjusts transaction timing accordingly
