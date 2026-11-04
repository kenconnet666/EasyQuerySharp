-- Java 的文档示例实体（doc/entity、entity/m2m）在 Java 测试里由 code-first 建表，这里直接给出 DDL
create table doc_user
(
    id varchar(32) not null primary key,
    name varchar(50) null,
    phone varchar(50) null,
    age int null
);
create table doc_bank
(
    id varchar(32) not null primary key,
    name varchar(50) null
);
create table doc_bank_card
(
    id varchar(32) not null primary key,
    uid varchar(32) null,
    code varchar(50) null,
    type varchar(50) null,
    bank_id varchar(32) null
);
create table doc_part
(
    id varchar(32) not null primary key,
    card_id varchar(32) null,
    name varchar(50) null
);
create table doc_user_book
(
    id varchar(32) not null primary key,
    uid varchar(32) null,
    name varchar(50) null,
    price decimal(16, 2) null
);
create table t_user_account
(
    id varchar(32) not null primary key,
    uid varchar(32) null,
    name varchar(50) null,
    create_time datetime null
);
create table t_user_book
(
    id varchar(32) not null primary key,
    uid varchar(32) null,
    name varchar(50) null,
    price decimal(16, 2) null
);
